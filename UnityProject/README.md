# Persia War — Unity 2.5D

بازسازی بازی Persia War با Unity در شاخه مستقل `unity-2.5d`.

## وضعیت فعلی
- Unity project skeleton
- Scene اولیه `Assets/Scenes/PersiaWarPrototype.unity`
- دوربین 2.5D و دنبال‌کردن بازیکن
- حرکت بازیکن با ورودی موبایل و Keyboard در Editor
- محدوده جهان 1056×1056
- زمین و مدل تستی بازیکن به‌صورت runtime ساخته می‌شوند تا Scene به Meshهای داخلی وابسته نباشد
- پایه Combat/Weapon برای مرحله بعد

## Asset migration
Assetهای اصلی Android در `main` باقی مانده‌اند و منبع مهاجرت هستند؛ از جمله Sprite Sheet پادشاه و پکیج‌های محیطی. طبق manifest اصلی، `king_sprite_sheet.png` برابر 6144×4096 و شامل 6 فریم در 4 جهت است.

## اجرا
1. پوشه `UnityProject` را به‌عنوان پروژه در Unity 2022.3 LTS باز کنید.
2. Scene `Assets/Scenes/PersiaWarPrototype.unity` را باز کنید.
3. Play را بزنید؛ در Editor با WASD/Arrow حرکت کنید.
4. در Android ورودی لمسی از `MobileInputHub` استفاده می‌کند.

## محدودیت این مرحله
خروجی APK در این محیط ساخته نشده است، چون Unity Editor و Android Build Support داخل محیط اجرای فعلی در دسترس نیست. کد و ساختار روی شاخه GitHub آماده شده‌اند تا در Unity Build شوند.
