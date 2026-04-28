# دليل المطورين (Developer Guide) 🛠️

مرحباً بك في فريق تطوير **STORM**!
هذا الدليل يحتوي على كل ما تحتاجه لفهم هيكلية المشروع، التحديات الحالية، وخطة العمل (Roadmap) لضمان تطوير آمن ومستقر. تم استخلاص هذا الدليل من الملاحظات الأساسية للمشروع لتسهيل العمل الجماعي.

## 1. نظرة عامة على التقنيات
- **المحرك:** Unity
- **الشبكات (Networking):** Photon Fusion (بأسلوب TPS/BR)
- **الخدمات الخلفية (Backend):** PlayFab (يُستخدم حالياً لنظام الأصدقاء والمصادقة)

## 2. نظام الأصدقاء الحالي (Friends System)
النظام يعتمد على PlayFab لجلب البيانات و Fusion للتعامل مع تدفق الشبكة.

### أهم المسارات والملفات:
- **السكربتات الأساسية:**
  - `Assets/TPSBR/Scripts/UI/Friends/PlayFabManager.cs` (المسؤول عن تسجيل الدخول وإدارة حالة الاتصال)
  - `Assets/TPSBR/Scripts/UI/Friends/UIFriendController.cs` (المتحكم الرئيسي بواجهة الأصدقاء)
  - `Assets/TPSBR/Scripts/UI/Friends/UIFriendsView.cs`
- **أدوات المحرر (Editor Scripts):**
  - `Assets/TPSBR/Scripts/Editor/FriendUIBuilder.cs` (يستخدم لبناء وتحديث واجهة الأصدقاء)
  - `Assets/TPSBR/Scripts/Editor/PlayFabAutoSetup.cs`
- **الواجهات (Prefabs):**
  - `Assets/TPSBR/UI/Prefabs/GeneralViews/UIFriendsView.prefab`
  - `Assets/TPSBR/UI/Prefabs/FriendItem.prefab`

### كيف يعمل النظام حالياً:
1. يبدأ `PlayFabManager` (موجود كـ Singleton في مشهد `Menu.unity`) بتسجيل الدخول التلقائي.
2. عند النجاح، يجلب الـ `PlayFabId`، `DisplayName`، `Avatar`، ويُحدث حالة اللاعب (Online).
3. يقوم `UIFriendController` بالاشتراك في أحداث التحديث (`OnFriendsUpdated`, `OnPlayersDiscovered`).
4. يتم إنشاء عنصر (`FriendItem.prefab`) لكل صديق، وتُطلب حالته (Online/Offline/InGame) بشكل منفصل.

## 3. المشاكل الحالية والحلول التقنية (Known Issues)

المشكلة الأساسية الحالية تكمن في **عدم استقرار واجهة المستخدم (UI)** الخاصة بالأصدقاء (تظهر وتختفي أحياناً). الأسباب والحلول المعتمدة:

- **السبب 1: الاعتماد على ترتيب Hierarchy الهش.**
  - *المشكلة:* يتم الوصول للنصوص عبر `texts[0]` و `texts[1]` مما ينكسر مع أي تعديل بسيط في الـ Prefab.
  - *الحل المطلوب:* إنشاء مكون `UIFriendItemView` يحتوي على مراجع مباشرة (NameText, StatusText, AvatarImage, الخ) واستخدامه في `UIFriendController`.
- **السبب 2: طلبات شبكة منفصلة لكل صديق (N+1 Problem).**
  - *المشكلة:* يؤدي لبطء واستهلاك عالي لحدود PlayFab المجانية (Free Tier).
  - *الحل المطلوب:* عمل Cache للحالة، تطبيق Refresh متدرج، وتجميع الطلبات (Batching) عبر CloudScript.
- **السبب 3: قصور في سكربت الـ Editor (`FriendUIBuilder.cs`).**
  - *المشكلة:* مسار إنشاء واجهة جديدة (`CreateUI`) غير مكتمل مقارنة بمسار التحديث (`UpdateExistingUI`).
  - *الحل المطلوب:* إعادة كتابة السكربت ليعمل بنمط `CreateOrUpdate` حتمي (Deterministic) مع `Validator` يطبع تقارير واضحة في الـ Console لأي مراجع ناقصة.

## 4. قواعد العمل الآمن (Development Guidelines)

لتجنب كسر المشروع أو فقدان البيانات، يرجى الالتزام بالآتي:
1. **لا تحذف ملفات `.meta` للبريفابات أبداً!** (مثل `UIFriendsView.prefab` و `FriendItem.prefab`). حافظ على نفس الـ GUID وقم بتحديث المحتوى الداخلي فقط لمنع انكسار الروابط في المشاهد.
2. **اصنع نسخ احتياطية:** قبل تعديل أي Prefab معقد، خذ نسخة احتياطية واضحة في مجلد `Assets/TPSBR/UI/Prefabs/_Backup/`.
3. **فصل المسؤوليات:** لا تقم بدمج منطق الواجهة (UI) مع منطق الشبكة (Network) أو البناء (Build) في سكربت واحد.
4. **التحقق التلقائي (Validation):** أي خطوة تغير في الـ Prefab يجب أن يتبعها Validator للتأكد من سلامة المراجع (References).
5. **التصميم قبل التحسين:** لا تبدأ بتحسين الشكل الجمالي للواجهة قبل تثبيت البنية المنطقية (Logic Wiring) أولاً.

## 5. نظرة مستقبلية: نظام البناء (Ghost Placement)
- لا توجد حالياً سكربتات بناء/Ghost فعلية في هذا المستودع.
- عند البدء في إضافة نظام البناء وإعطاء "صلاحيات بناء للأصدقاء"، تأكد من تنفيذ تحقق مزدوج (Double Validation):
  1. **Client-side:** فحص الواجهة لتحسين تجربة المستخدم ومنع التقطيع.
  2. **Server/Host-side:** فحص نهائي قبل التنفيذ الفعلي لمنع الغش (Anti-cheat).

## 6. خطة التنفيذ المباشرة (Immediate Action Plan - MVP)
إذا كنت تبحث عن مهمة للبدء بها، اتبع هذا الترتيب المقترح:
1. **[أولوية قصوى]** توحيد وتثبيت مراجع واجهة عنصر الصديق (`UIFriendItemView`).
2. إعادة كتابة `FriendUIBuilder.cs` بنمط (CreateOrUpdate + Validate).
3. تقليل نداءات الشبكة لحالة الأصدقاء (تطبيق سياسة Cache + Refresh Policy).
4. إضافة نظام دعوات (Invite flow) عبر وظائف PlayFab CloudScript (مثل `sendBuildInvite`, `acceptBuildInvite`).
5. اختبار شامل لثلاث حالات أساسية: (قائمة فارغة، قائمة قصيرة، قائمة كبيرة مع تأخير في الشبكة).

## 7. قائمة تحقق الجودة (QA Checklist)
قبل اعتماد (Commit/Push) أي تعديلات على نظام الأصدقاء، تأكد من الآتي:
- [ ] فتح لوحة الأصدقاء من الـ Menu ومن الـ Gameplay يعمل بدون أخطاء.
- [ ] عمل Refresh متكرر لا يسبب اختفاء العناصر.
- [ ] إضافة/إزالة صديق يحدث فوراً في الواجهة.
- [ ] الـ Avatar والاسم والحالة تطابق الحساب الصحيح.
- [ ] لا تظهر أخطاء `NullReferenceException` في الـ Console أثناء إنشاء العناصر.

---
*تم إعداد هذا الدليل ليكون مرجعاً عملياً لكل مطور ينضم إلى المشروع. شكراً لمساهمتك في تطوير STORM!*
