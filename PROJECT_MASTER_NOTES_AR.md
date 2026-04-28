# تقرير شامل للمشروع (مفهوم للجميع)

هذا الملف يجمع كل ما تم فهمه من المشروع حتى الآن، خصوصا نظام الأصدقاء وربطه مع الشبكة، مع ملاحظات عملية وخطة تطوير آمنة.

## 1) ملخص سريع جدا

- المشروع: لعبة Unity مبنية على Photon Fusion (TPS/BR style).
- يوجد نظام أصدقاء باستخدام PlayFab.
- المشكلة الأساسية الحالية ليست في "البيانات" نفسها، بل في استقرار عرض واجهة الأصدقاء (UI) وطريقة التحديث.
- يوجد سكربت Editor يبني/يعدل واجهة الأصدقاء، لكنه غير متماسك بالكامل في مسار الإنشاء.
- لا يوجد حاليا نظام بناء/Ghost Placement فعلي داخل هذا الريبو (لا توجد سكربتات Build/Ghost واضحة).

## 2) أهم المسارات والملفات في المشروع

### نظام الأصدقاء وPlayFab

- `Assets/TPSBR/Scripts/UI/Friends/PlayFabManager.cs`
- `Assets/TPSBR/Scripts/UI/Friends/UIFriendController.cs`
- `Assets/TPSBR/Scripts/UI/Friends/UIFriendsView.cs`
- `Assets/TPSBR/Scripts/Editor/FriendUIBuilder.cs`
- `Assets/TPSBR/Scripts/Editor/PlayFabAutoSetup.cs`
- `Assets/TPSBR/UI/Prefabs/GeneralViews/UIFriendsView.prefab`
- `Assets/TPSBR/UI/Prefabs/FriendItem.prefab`

### ربط PlayFab مع تدفق الشبكة (Fusion)

- `Assets/TPSBR/Scripts/Networking/Networking.cs`
  - يتم استدعاء `PlayFabManager.SetInGame(...)` عند الدخول.
  - يتم استدعاء `PlayFabManager.SetInMenu()` عند الخروج/العودة.

### وجود PlayFabManager في المشهد

- `Assets/TPSBR/Scenes/Menu.unity` فيه GameObject باسم `PlayFabManager`.

## 3) كيف نظام الأصدقاء يعمل حاليا

1. `PlayFabManager` يعمل Singleton ويبدأ Login تلقائيا.
2. عند نجاح Login:
- يجلب PlayFabId وDisplayName وAvatar.
- يحدث الحالة Online.
- يجلب قائمة الأصدقاء.
3. `UIFriendController` يشترك في events:
- `OnFriendsUpdated`
- `OnPlayersDiscovered`
- `OnStatusMessage`
4. لكل عنصر صديق في القائمة:
- يتم إنشاء Item من `FriendItem.prefab`.
- يتم طلب حالة الصديق (Online/Offline/InGame) عبر `GetUserData` بشكل منفصل لكل صديق.

## 4) لماذا الواجهة تظهر أحيانا وتختفي أحيانا (الأسباب الفعلية)

### السبب 1: اعتماد هش على ترتيب النصوص داخل prefab

في `UIFriendController` يتم استخدام:
- `GetComponentsInChildren<TMP_Text>(true)`
- ثم الوصول بـ `texts[0]` و `texts[1]`

هذا يعتمد على ترتيب Hierarchy داخلي يمكن يتغير مع أي تعديل بسيط في البريفاب، فينكسر العرض فجأة.

### السبب 2: طلب شبكة منفصل لكل صديق

لكل صديق يتم استدعاء `GetFriendStatus` -> `GetUserData`.
لو عندك أصدقاء كثير أو شبكة بطيئة، النتائج ترجع متأخرة/متقطعة، فتشوف UI غير ثابت.

### السبب 3: تبديل تلقائي للتبويب

عند عدم وجود أصدقاء، الكود يحول تلقائيا إلى Discover tab. للمستخدم قد يظهر كأن "قائمة الأصدقاء اختفت".

### السبب 4: سكربت Editor نفسه فيه ملاحظة أنه مختصر

داخل `FriendUIBuilder.cs` يوجد تعليق صريح أن منطق الإنشاء الكامل غير مكتمل مثل التحديث.
هذا يزيد احتمال اختلاف بين "UI منشأ" و"UI متوقع من الكود".

## 5) تقييم سكربت الـEditor المسؤول عن لوحة الأصدقاء

السكريبت الحالي: `Assets/TPSBR/Scripts/Editor/FriendUIBuilder.cs`

- فيه أمر لتحديث UI موجود: `UpdateExistingUI()`.
- فيه أمر لإنشاء UI جديد: `CreateUI()`.
- في مسار الإنشاء لا يوجد ضمان صريح لإضافة `UIFriendsView` و`UIFriendController` وربط كل الحقول بنفس صلابة مسار التحديث.
- لذلك الاعتماد عليه لإنشاء جديد كامل قد ينتج واجهة تعمل شكليا لكن غير مستقرة وظيفيا.

## 6) كيف نعيد كتابة السكربت والبريفاب بدون فقد بيانات

## مبدأ الأمان الأساسي

- لا نحذف `.meta` للبريفابات.
- نحافظ على نفس asset GUID للبريفابات قدر الإمكان:
  - `UIFriendsView.prefab`
  - `FriendItem.prefab`
- نحدث المحتوى الداخلي فقط.

## خطوات التنفيذ الموصى بها

1. إنشاء مكوّن عرض عنصر صديق واضح (مثال: `UIFriendItemView`) فيه مراجع مباشرة:
- NameText
- StatusText
- AvatarImage
- JoinButton
- RemoveButton
- StatusIndicator

2. تعديل `UIFriendController` لاستخدام هذا المكوّن بدلا من `texts[0]/texts[1]`.

3. إعادة كتابة `FriendUIBuilder` ليقوم فقط بـ:
- `CreateOrUpdate` بشكل deterministic.
- `ValidateReferences` بعد البناء.
- طباعة تقرير أخطاء واضح في Console لو أي مرجع ناقص.

4. إنشاء نسخة احتياطية قبل التحديث:
- نسخة prefab احتياطية باسم واضح داخل `Assets/TPSBR/UI/Prefabs/_Backup/`.

5. اختبار يدوي بعد كل خطوة (فتح Menu + GameplayUI + فتح لوحة الأصدقاء + Refresh).

## 7) ما الذي لن نفقده إذا طبقنا الخطوات صح

- بيانات PlayFab للمستخدمين (Friends, UserData, DisplayName, Avatar) محفوظة على السحابة.
- الروابط من المشاهد للبريفاب لن تنكسر إذا لم نحذف `.meta`.
- المشكلة هنا UI wiring وليست data migration.

## 8) PlayFab Free Tier (ملاحظة مهمة)

- النظام الحالي قابل للعمل على PlayFab Free/Dev لكن يجب تقليل الاستدعاءات.
- حاليا طلب حالة لكل صديق بشكل منفصل يستهلك Reads بسرعة.
- الحل الأفضل:
  - Cache للحالة.
  - Refresh متدرج.
  - تجميع طلبات الحالة عبر CloudScript إذا لزم.

مهم: حدود الباقة المجانية تتغير بمرور الوقت، راجع دائما وثائق PlayFab الرسمية قبل الإطلاق.

## 9) ملاحظة عن "نظام البناء + Ghost"

- بعد الفحص الحالي: لا توجد سكربتات بناء/Ghost واضحة داخل هذا الريبو.
- لذلك أي خطة ربط "صلاحيات بناء للأصدقاء" حاليا تعتبر تصميم مستقبلي حتى تضيف ملفات البناء فعليا.
- عند إضافة البناء لاحقا، نفذ تحقق صلاحية مرتين:
  - فحص UI/Client لتحسين التجربة.
  - فحص Server/Host قبل التنفيذ الفعلي لمنع الغش.

## 10) أفضل تصميم عملي للأصدقاء (النسخة القابلة للتنفيذ)

### مفاتيح UserData مقترحة

- `presence_state`: `online|in_menu|in_match|offline`
- `presence_session`: Session/Room code
- `presence_last_seen_utc`: وقت UTC
- `friend_build_rights`: `all|none|listed`
- `friend_build_list`: قائمة IDs (JSON)

### وظائف CloudScript المقترحة

- `sendBuildInvite`
- `acceptBuildInvite`
- `setBuildPermission`
- `batchFriendPresence`

## 11) خطة تنفيذ MVP (مرتبة)

1. تثبيت UI Friend Item references (أولوية قصوى).
2. إعادة كتابة `FriendUIBuilder` مع Validator.
3. تقليل نداءات status لكل صديق (cache + refresh policy).
4. إضافة Invite flow عبر CloudScript.
5. إضافة صلاحيات البناء عند توفر نظام البناء.

## 12) قائمة تحقق QA قبل اعتماد التعديلات

- فتح لوحة الأصدقاء من Menu ومن Gameplay بدون أخطاء.
- Refresh متكرر لا يسبب اختفاء العناصر.
- Add/Remove friend يحدث فورا بالواجهة.
- avatar/name/status يطابق الحساب الصحيح.
- إغلاق اللعبة/العودة يحدث Presence منطقي.
- لا توجد NullReferenceException في Console أثناء إنشاء العناصر.

## 13) قرارات إدارية مهمة (لتجنب أخطاء مبتدئين)

- لا تبدأ بتحسين الشكل قبل تثبيت البنية المنطقية.
- لا تستخدم ترتيب الأبناء داخل prefab كاعتماد منطقي.
- لا تربط أكثر من مسؤولية داخل نفس سكربت (UI بناء + Logic + Network) بدون فصل واضح.
- أي خطوة تغير prefab يجب أن يتبعها Validator تلقائي.

## 14) ماذا نفعل الآن مباشرة

1. توحيد عقدة بيانات عنصر الصديق (`UIFriendItemView`) وربطها صراحة.
2. إعادة كتابة `FriendUIBuilder` بنمط CreateOrUpdate + Validate.
3. اختبار 3 حالات أساسية: قائمة فارغة، قائمة قصيرة، قائمة كبيرة مع network delay.

---

هذا التقرير مكتوب ليكون مرجع عملي للفريق كله.
