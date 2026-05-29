import json, re

# Extract current EN and PL from i18n.js
with open('wwwroot/js/i18n.js', 'r', encoding='utf-8') as f:
    data = f.read()

def extract_section(name):
    start = data.find(f'"{name}":')
    brace_start = data.find('{', start)
    brace_count = 0
    end = brace_start
    for i, ch in enumerate(data[brace_start:], brace_start):
        if ch == '{':
            brace_count += 1
        elif ch == '}':
            brace_count -= 1
            if brace_count == 0:
                end = i
                break
    return json.loads(data[brace_start:end+1])

EN = extract_section('en')
PL = extract_section('pl')

# Launch Options overlay keys
LAUNCH_EN = {
    "lp_force_d3d11": "Force Direct3D 11", "lp_force_d3d11_desc": "Force DX11 instead of DX12.",
    "lp_force_vulkan": "Force Vulkan", "lp_force_vulkan_desc": "Use Vulkan API (experimental).",
    "lp_force_opengl": "Force OpenGL", "lp_force_opengl_desc": "Use OpenGL (for older GPUs).",
    "lp_nographics": "No Graphics", "lp_nographics_desc": "Launch without graphics (for server).",
    "lp_batchmode": "Batch Mode", "lp_batchmode_desc": "Background mode without UI.",
    "lp_screen_width": "Screen Width", "lp_screen_width_desc": "Window width at launch.",
    "lp_screen_height": "Screen Height", "lp_screen_height_desc": "Window height at launch.",
    "lp_screen_fullscreen": "Fullscreen", "lp_screen_fullscreen_desc": "Launch in fullscreen mode.",
    "lp_screen_refresh_rate": "Refresh Rate", "lp_screen_refresh_rate_desc": "Screen refresh rate.",
    "lp_popupwindow": "Popup Window", "lp_popupwindow_desc": "Borderless window.",
    "lp_windowed": "Windowed", "lp_windowed_desc": "Windowed mode.",
    "lp_single_instance": "Single Instance", "lp_single_instance_desc": "Prevent multiple game copies.",
    "lp_force_cpu_count": "CPU Count", "lp_force_cpu_count_desc": "Limit Unity thread count.",
    "lp_force_job_worker_count": "Job Workers", "lp_force_job_worker_count_desc": "Number of Job System workers.",
    "lp_gc_boehm": "GC Boehm", "lp_gc_boehm_desc": "Use Boehm GC instead of IL2CPP default.",
    "lp_disable_gpu_skinnin": "Disable GPU Skinning", "lp_disable_gpu_skinnin_desc": "Disable GPU skinning.",
    "lp_weak_http_security": "Weak HTTP Security", "lp_weak_http_security_desc": "RKN bypass (reduced HTTP security).",
    "lp_log_file": "Log File", "lp_log_file_desc": "Path for Unity log output.",
}
LAUNCH_RU = {
    "lp_force_d3d11": "Форсировать DirectX 11", "lp_force_d3d11_desc": "Принудительно использовать DX11 вместо DX12.",
    "lp_force_vulkan": "Форсировать Vulkan", "lp_force_vulkan_desc": "Использовать Vulkan API (экспериментально).",
    "lp_force_opengl": "Форсировать OpenGL", "lp_force_opengl_desc": "Использовать OpenGL (для старых GPU).",
    "lp_nographics": "Без графики", "lp_nographics_desc": "Запуск без графики (для сервера).",
    "lp_batchmode": "Batch Mode", "lp_batchmode_desc": "Фоновый режим без UI.",
    "lp_screen_width": "Ширина экрана", "lp_screen_width_desc": "Ширина окна при запуске.",
    "lp_screen_height": "Высота экрана", "lp_screen_height_desc": "Высота окна при запуске.",
    "lp_screen_fullscreen": "Полноэкранный", "lp_screen_fullscreen_desc": "Запуск в полноэкранном режиме.",
    "lp_screen_refresh_rate": "Частота обновления", "lp_screen_refresh_rate_desc": "Частота обновления экрана.",
    "lp_popupwindow": "Всплывающее окно", "lp_popupwindow_desc": "Окно без рамки.",
    "lp_windowed": "Оконный режим", "lp_windowed_desc": "Оконный режим.",
    "lp_single_instance": "Один экземпляр", "lp_single_instance_desc": "Запретить несколько копий игры.",
    "lp_force_cpu_count": "Количество CPU", "lp_force_cpu_count_desc": "Ограничить количество потоков Unity.",
    "lp_force_job_worker_count": "Job Workers", "lp_force_job_worker_count_desc": "Количество Job System воркеров.",
    "lp_gc_boehm": "GC Boehm", "lp_gc_boehm_desc": "Использовать Boehm GC вместо IL2CPP дефолтного.",
    "lp_disable_gpu_skinnin": "Отключить GPU Skinning", "lp_disable_gpu_skinnin_desc": "Отключить скиннинг на GPU.",
    "lp_weak_http_security": "Слабая HTTP безопасность", "lp_weak_http_security_desc": "Обход блокировок РКН (пониженная безопасность HTTP).",
    "lp_log_file": "Файл логов", "lp_log_file_desc": "Путь для записи логов Unity.",
}
LAUNCH_PL = {
    "lp_force_d3d11": "Wymuś DirectX 11", "lp_force_d3d11_desc": "Wymuś DX11 zamiast DX12.",
    "lp_force_vulkan": "Wymuś Vulkan", "lp_force_vulkan_desc": "Użyj API Vulkan (eksperymentalne).",
    "lp_force_opengl": "Wymuś OpenGL", "lp_force_opengl_desc": "Użyj OpenGL (dla starszych GPU).",
    "lp_nographics": "Bez grafiki", "lp_nographics_desc": "Uruchom bez grafiki (dla serwera).",
    "lp_batchmode": "Tryb wsadowy", "lp_batchmode_desc": "Tryb tła bez UI.",
    "lp_screen_width": "Szerokość ekranu", "lp_screen_width_desc": "Szerokość okna przy uruchomieniu.",
    "lp_screen_height": "Wysokość ekranu", "lp_screen_height_desc": "Wysokość okna przy uruchomieniu.",
    "lp_screen_fullscreen": "Pełny ekran", "lp_screen_fullscreen_desc": "Uruchom w trybie pełnoekranowym.",
    "lp_screen_refresh_rate": "Odświeżanie", "lp_screen_refresh_rate_desc": "Częstotliwość odświeżania ekranu.",
    "lp_popupwindow": "Okno popup", "lp_popupwindow_desc": "Okno bez ramki.",
    "lp_windowed": "Okno", "lp_windowed_desc": "Tryb okienny.",
    "lp_single_instance": "Jeden egzemplarz", "lp_single_instance_desc": "Zapobiegaj wielu kopiom gry.",
    "lp_force_cpu_count": "Liczba CPU", "lp_force_cpu_count_desc": "Ogranicz liczbę wątków Unity.",
    "lp_force_job_worker_count": "Job Workers", "lp_force_job_worker_count_desc": "Liczba wątków Job System.",
    "lp_gc_boehm": "GC Boehm", "lp_gc_boehm_desc": "Użyj Boehm GC zamiast domyślnego IL2CPP.",
    "lp_disable_gpu_skinnin": "Wyłącz GPU Skinning", "lp_disable_gpu_skinnin_desc": "Wyłącz skinning na GPU.",
    "lp_weak_http_security": "Słabe HTTP", "lp_weak_http_security_desc": "Obejście blokad RKN (zmniejszone bezpieczeństwo HTTP).",
    "lp_log_file": "Plik logów", "lp_log_file_desc": "Ścieżka do logów Unity.",
}

# Full manual Russian translations
RU = {
    "account": "Аккаунт",
    "tab_home": "Главная",
    "tab_graphics": "Графика",
    "tab_gameplay": "Геймплей",
    "tab_audio": "Аудио",
    "tab_launch": "Запуск",
    "tab_cheats": "Читы",
    "tab_system": "Система",
    "tab_network": "Сеть",
    "tab_settings": "Настройки",
    "tab_performance": "Производительность",
    "tab_advanced": "Продвинутое",
    "tab_tools": "Инструменты",
    "graphics_presets": "Пресеты графики",
    "preset_max_fps": "Макс FPS",
    "preset_balanced": "Баланс",
    "preset_quality": "Качество",
    "visual_settings": "Визуальные настройки",
    "draw_distance": "Дальность прорисовки",
    "camera_fog": "Туман камеры",
    "texture_quality": "Качество текстур",
    "anti_aliasing": "Сглаживание",
    "shadow_quality": "Качество теней",
    "fps_limit": "Лимит FPS (0 = выкл)",
    "effects": "Эффекты",
    "shadows": "Тени",
    "bloom": "Bloom",
    "ssao": "SSAO",
    "motion_blur": "Размытие движения",
    "ambient_occlusion": "Ambient Occlusion",
    "depth_of_field": "Глубина резкости",
    "chromatic_aberration": "Хроматическая аберрация",
    "vsync": "Вертикальная синхронизация",
    "show_blood": "Показывать кровь",
    "ragdoll_cleanup": "Очистка рэгдоллов",
    "flashlight_shadows": "Тени фонарика",
    "ragdoll_limit": "Лимит рэгдоллов",
    "audio_levels": "Уровни звука",
    "master_volume": "Общая громкость",
    "sfx_volume": "Громкость эффектов",
    "voice_volume": "Громкость голоса",
    "music_volume": "Громкость музыки",
    "voice": "Голосовой чат",
    "noise_suppression": "Подавление шума",
    "noise_suppression_desc": "Отключите для меньшей нагрузки на CPU",
    "voice_ns_level": "Уровень подавления шума",
    "steam_launch_options": "Параметры запуска Steam",
    "force_dx11": "Форсировать DirectX 11",
    "fastest_quality": "Макс. производительность",
    "windowed_borderless": "Оконный без рамок",
    "disable_gpu_skinning": "Отключить GPU Skinning",
    "force_vulkan": "Форсировать Vulkan",
    "force_opengl": "Форсировать OpenGL",
    "no_log": "Без логов",
    "no_joy": "Отключить джойстик",
    "high_priority_launch": "Высокий приоритет",
    "malloc_system": "Системный аллокатор",
    "use_all_cores": "Использовать все ядра",
    "custom_args": "Кастомные аргументы",
    "save_launch_options": "Сохранить параметры запуска",
    "launch_game": "Запустить игру",
    "layer1_file_nukes": "Слой 1 — Модификация файлов",
    "safe": "БЕЗОПАСНО",
    "layer1_desc": "Изменяет файлы игры на диске ДО запуска. Античит ещё не работает — он этого не видит. Steam может откатить файлы после обновления; примените повторно.",
    "anticheat_bypass": "Обход античита",
    "anticheat_bypass_desc": "Заменяет SL-AC.dll на заглушку. Античит не загружается.",
    "disable_crash_handler": "Отключить обработчик крашей",
    "disable_crash_handler_desc": "Запрещает запуск UnityCrashHandler. Меньше фоновых процессов.",
    "disable_analytics": "Отключить аналитику",
    "disable_analytics_desc": "Удаляет Unity analytics DLL. Останавливает сбор телеметрии.",
    "bootconfig_nuke": "Boot.config мод",
    "bootconfig_nuke_desc": "Агрессивный GC, без single-instance блокировки, многопоточный рендер.",
    "ggm_destroyer": "GlobalGameManagers мод",
    "ggm_destroyer_desc": "Бинарный патч Unity QualitySettings. Ноль теней, без AA, без vsync, агрессивный LOD.",
    "layer1b_hex": "Слой 1b — Hex GameAssembly",
    "nuclear": "ЯДЕРНЫЙ",
    "layer1b_desc": "Хекс-патчит скомпилированный IL2CPP бинарник (GameAssembly.dll) на диске. Неправильный паттерн = мгновенный краш. Обновляйте паттерны через x64dbg.",
    "analyze_binary": "Анализировать бинарник",
    "apply_hex_patches": "Применить хекс-патчи",
    "restore_backup": "Восстановить бэкап",
    "layer2_launch": "Слой 2 — Скрытый запуск",
    "layer2_desc": "Подготовка + запуск в один клик. Опционально самоуничтожение твикера после запуска, чтобы античит не нашёл процесс.",
    "patch_ac": "Патч античита",
    "patch_ac_desc": "Включить замену SL-AC.dll",
    "patch_boot": "Патч Boot.config",
    "patch_boot_desc": "Включить твики движка Unity",
    "patch_ggm": "Патч GlobalGameManagers",
    "patch_ggm_desc": "Включить бинарный патч QualitySettings",
    "self_destruct": "Самоуничтожение после запуска",
    "self_destruct_desc": "Убивает твикер через 3 секунды после принятия команды запуска Steam",
    "prepare_env": "Подготовить окружение",
    "launch_stealth": "Запустить скрытно",
    "restore_all_files": "Восстановить все файлы",
    "layer3_memory": "Слой 3 — Память во время игры",
    "unsafe": "ОПАСНО",
    "layer3_desc": "Требует, чтобы античит УЖЕ БЫЛ ОТКЛЮЧЁН. Открывает хендл к запущенной игре и патчит память. Ядерный античит = мгновенный бан. Только для офлайн/приватных серверов.",
    "load_patch_db": "Загрузить базу патчей",
    "apply_all_ready": "Применить все готовые",
    "process_optimization": "Оптимизация процессов",
    "high_priority": "Высокий приоритет",
    "high_priority_desc": "Установить SCPSL.exe в High",
    "realtime": "Realtime",
    "realtime_desc": "Максимальный приоритет планирования",
    "physical_cores": "Физические ядра",
    "physical_cores_desc": "Отключить HT/SMT ядра",
    "clear_ram": "Очистить RAM",
    "clear_ram_desc": "Очистить рабочие наборы",
    "timer_05ms": "Таймер 0.5мс",
    "timer_05ms_desc": "Снизить задержку ввода",
    "kill_bloat": "Убить хлам",
    "kill_bloat_desc": "Остановить ненужные сервисы",
    "system_tweaks": "Системные твики",
    "persistent": "ПОСТОЯННЫЕ",
    "game_mode": "Игровой режим",
    "game_mode_desc": "Windows Game Mode для приоритета foreground",
    "disable_gamebar": "Отключить Game Bar",
    "disable_gamebar_desc": "Отключает Xbox оверлей и DVR",
    "disable_core_parking": "Отключить Core Parking",
    "disable_core_parking_desc": "Держит ядра CPU активными",
    "high_perf_plan": "План высокой производительности",
    "high_perf_plan_desc": "Максимальная частота CPU",
    "disable_fso": "Отключить Fullscreen Optimizations",
    "disable_fso_desc": "Классический фулскрин для SCPSL.exe",
    "disable_hpet": "Отключить HPET",
    "disable_hpet_desc": "Меньшая задержка таймера для игр",
    "disable_nagle": "Отключить Nagle",
    "disable_nagle_desc": "TCP NoDelay для меньшего пинга",
    "disable_sysmain": "Отключить SysMain",
    "disable_sysmain_desc": "Остановить фоновый ввод-вывод Superfetch",
    "disable_visual_effects": "Отключить визуальные эффекты",
    "disable_visual_effects_desc": "Режим максимальной производительности Windows",
    "rkn_bypass": "Обход РКН",
    "bypass_not_active": "Обход не активен",
    "bypass_active": "Обход АКТИВЕН",
    "zapret_detected": "Zapret обнаружен",
    "network_diagnostics": "Сетевая диагностика",
    "network_diagnostics_desc": "Проверка связи с игровыми серверами, DNS и обнаружение конфликтов с другими обходами.",
    "run_diagnostics": "Запустить диагностику",
    "auto_reset_rkn": "Авто-сброс при отсутствии интернета",
    "report_issue_desc": "Опишите проблему. Будет создан полный диагностический отчёт и отправлен в Discord.",
    "report_placeholder": "Что пошло не так?",
    "rkn_desc": "Маршрутизирует трафик Steam и SCP:SL в обход блокировок российских провайдеров через Cloudflare DNS и кастомные записи hosts.",
    "rkn_analysis": "Анализ обхода",
    "rkn_analysis_desc": "Системный анализ для поиска лучшего метода обхода для вашей сети.",
    "analyze_system": "Анализировать систему",
    "generate_zapret": "Сгенерировать конфиг Zapret",
    "custom_hosts_title": "Кастомные хосты",
    "custom_hosts_desc": "Добавьте свои записи hosts (по одной на строку: IP домен).",
    "apply_custom_hosts": "Применить кастомные хосты",
    "reset_custom_hosts": "Сбросить кастомные",
    "activate_bypass": "Активировать обход",
    "deactivate": "Деактивировать",
    "what_changed": "Что изменено",
    "custom_hosts": "Кастомные записи hosts",
    "cloudflare_dns": "Cloudflare DNS (1.1.1.1)",
    "winhttp_proxy": "Сброс WinHTTP прокси",
    "app_info": "Информация о приложении",
    "version": "Версия",
    "game_path": "Путь к игре",
    "game_status": "Статус игры",
    "backup_manager": "Менеджер бэкапов",
    "create_backup": "Создать бэкап",
    "restore_latest": "Восстановить последний",
    "report_issue": "Сообщить о проблеме",
    "generate_report": "Сгенерировать отчёт",
    "send_report_discord": "Отправить в Discord",
    "discord_integration": "Интеграция Discord",
    "discord_integration_desc": "Отправка отчётов и уведомлений в канал Discord через вебхук.",
    "discord_logs_webhook": "Логи / Краши",
    "discord_reports_webhook": "Репорты игроков",
    "steam_auth": "Аккаунт Steam",
    "steam_auth_desc": "Войдите через Steam, чтобы сохранять настройки и получить доступ ко всем функциям.",
    "login_with_steam": "Войти через Steam",
    "logout": "Выйти",
    "steam_auth_pending": "Ожидание авторизации Steam...",
    "test_webhook": "Тест вебхука",
    "actions": "Действия",
    "open_game_folder": "Открыть папку игры",
    "refresh_status": "Обновить статус",
    "status_ready": "Готово",
    "status_unsaved": "Есть несохранённые изменения",
    "save_config": "Сохранить конфиг",
    "memory_no_shadows": "Без теней",
    "memory_no_shadows_desc": "NOP теневого пайплайна. Огромная экономия GPU.",
    "memory_no_fog": "Без тумана",
    "memory_no_fog_desc": "Убивает атмосферный туман.",
    "memory_fps_unlock": "Разблокировка FPS",
    "memory_fps_unlock_desc": "Снимает жёсткий лимит 60/120/144 FPS в главном цикле Unity.",
    "memory_no_postprocess": "Без пост-процесса",
    "memory_no_postprocess_desc": "Отключает Bloom, SSAO, Motion Blur, DoF одним махом.",
    "memory_fov": "Изменение FOV",
    "memory_fov_desc": "Патчит Camera::get_fieldOfView сеттер, чтобы форсировать любой FOV.",
    "memory_no_blood": "Без крови",
    "memory_no_blood_desc": "Отключает брызги крови и декали. Экономит GPU fill-rate и CPU физику.",
    "memory_no_ragdolls": "Без рэгдоллов",
    "memory_no_ragdolls_desc": "Убивает Ragdoll::Update. Тела мгновенно прилипают к земле вместо симуляции физики. Огромная экономия CPU в перестрелках.",
    "memory_no_particles": "Без частиц",
    "memory_no_particles_desc": "NOP ParticleSystem::Update и ::Render. Никакого дыма, искр, дульной вспышки, кровавого тумана.",
    "memory_no_reflections": "Без отражений",
    "memory_no_reflections_desc": "Отключает ReflectionProbe. Блестящие поверхности становятся матовыми. Большой выигрыш GPU на индор-картах.",
    "memory_fast_anim": "Быстрая анимация",
    "memory_fast_anim_desc": "Патчит Time::get_timeScale на 2.0. ВСЁ движется в 2 раза быстрее, включая вашего персонажа. Это чит, а не просто FPS.",
    "home_welcome": "Добро пожаловать в MatrixHole-Engine",
    "home_desc": "Оптимизируйте игру в 3 простых шага. Всё здесь безопасно — файлы не будут изменены, пока вы не перейдёте во вкладку Продвинутое.",
    "home_step1": "Выберите пресет графики",
    "recommended_badge": "РЕКОМЕНДУЕТСЯ",
    "preset_max_fps_desc": "Минимальное качество, максимальный FPS",
    "preset_balanced_desc": "Хорошее качество и производительность",
    "preset_quality_desc": "Лучшая картинка, меньше FPS",
    "home_step2": "Запустите игру",
    "home_step3": "Защитите настройки",
    "panic_reset_title": "Что-то пошло не так?",
    "panic_reset_desc": "Мгновенно отменить все изменения и восстановить оригинальные файлы игры. Используйте, если игра крашится или ведёт себя странно после твиков.",
    "full_reset_btn": "Откатить всё (Полный сброс)",
    "soft_reset_btn": "Мягкий сброс (Только отключить твики)",
    "quick_tools": "Быстрые инструменты",
    "advanced_graphics": "Продвинутая графика",
    "safe_tweaks": "Быстрый буст производительности",
    "windows_game_tweaks": "Игровые твики Windows",
    "file_modifications": "Модификация файлов",
    "patch_game_binary": "Патч бинарника игры",
    "stealth_launch": "Скрытый запуск",
    "runtime_memory_patches": "Патчи памяти во время игры",
    "gate_title": "Доступ по приглашению",
    "gate_desc": "Этот инструмент только для верифицированных членов Discord-сервера. Войдите через Discord, чтобы продолжить.",
    "discord_connect": "Подключить Discord",
    "discord_auth_desc": "Войдите через Discord для доступа к приложению.",
    "discord_disconnect": "Отключить",
    "steam_link_desc": "Привяжите Steam-аккаунт для полного функционала.",
    "link_steam": "Привязать Steam",
    "unlink_steam": "Отвязать",
    "language": "Язык",
    "profile_manager": "Менеджер профилей",
    "profile_manager_desc": "Сохраняйте и переключайтесь между пресетами конфигурации.",
    "diagnostics": "Диагностика",
    "disk_space": "Место на диске",
    "game_cleanup": "Очистка игры",
    "focus_assist": "Фокусировка",
    "focus_assist_desc": "Включите режим «Не беспокоить» перед игрой.",
    "enable_dnd": "Включить DND",
    "disable_dnd": "Отключить DND",
    "push_notifications": "Push-уведомления",
    "push_notifications_desc": "Следите за серверами и получайте уведомления о свободных слотах.",
    "conflict_detector": "Детектор конфликтов",
    "conflict_detector_desc": "Сканирование конфликтующего ПО: RTSS, Cheat Engine, x64dbg, Process Hacker.",
    "scan_now": "Сканировать",
    "benchmark": "Бенчмарк",
    "benchmark_desc": "Измерение FPS и frametime во время игры. Рекомендации по производительности.",
    "report_issue": "Сообщить о проблеме",
    "discord_rich_presence": "Rich Presence",
    "discord_rich_presence_desc": "Показывать игровой статус в профиле Discord.",
    "discord_not_connected": "Не подключено",
    "rkn_bypass": "Обход РКН",
    "what_changed": "Что изменено",
    "custom_hosts_title": "Кастомные хосты",
    "rkn_analysis": "Анализ обхода",
    "rkn_analysis_desc": "Системный анализ для поиска лучшего метода обхода.",
    "network_diagnostics": "Сетевая диагностика",
    "network_diagnostics_desc": "Проверка связи с игровыми серверами, DNS и обнаружение конфликтов.",
    "graphics_quality": "Качество графики",
    "shadow_distance": "Дальность теней",
    "shadow_cascades": "Каскады теней",
    "advanced": "Продвинутое",
    "lod_bias": "Смещение LOD",
    "particle_budget": "Лимит частиц",
    "anisotropic_filtering": "Анизотропная фильтрация",
    "soft_particles": "Мягкие частицы",
    "memory_lod": "Принудительный LOD (минимум)",
    "memory_lod_desc": "Принудительно выбирает самый низкий уровень детализации. Удалённые объекты становятся угловатыми, но рендерятся быстрее.",
    "tag_ready": "Готово",
    "tag_placeholder": "Заглушка",
    "apply_patch": "Применить патч",
    "tooltip_graphics_quality": "Общее качество рендеринга. Ниже = больше FPS.",
    "tooltip_draw_distance": "Насколько далеко видно. Меньше значения = значительный прирост FPS.",
    "tooltip_texture_quality": "Разрешение текстур. Ниже = меньше использование VRAM.",
    "tooltip_shadow_quality": "Детализация теней. Сильно влияет на FPS.",
    "tooltip_anti_aliasing": "Уровень MSAA. 0 = выкл, 2/4/8 = количество сэмплов. Выше = плавные края, меньше FPS.",
    "tooltip_fps_limit": "Ограничение макс. FPS. 0 = без ограничений. Помогает при перегреве.",
    "tooltip_vsync": "Синхронизирует FPS с частотой обновления монитора. Добавляет задержку ввода.",
    "tooltip_anisotropic_filtering": "Улучшает чёткость текстур под углом. Незначительная потеря FPS.",
    "tooltip_soft_particles": "Сглаживание краёв частиц. Отключите для большего FPS.",
    "tooltip_show_blood": "Эффекты брызг крови. Отключите для лучшей видимости.",
    "tooltip_ragdoll_cleanup": "Авто-удаление тел. Больше FPS в боях.",
    "tooltip_master_volume": "Общая громкость игры.",
    "tooltip_noise_suppression": "Фильтрует шум микрофона. Использует CPU.",
    "game_cleanup_desc": "Удалите временные файлы, чтобы освободить место на диске.",
    "clean": "Очистить",
    "clean_all": "Очистить всё",
    "create_profile": "Создать профиль",
    "refresh": "Обновить",
    "check_disk": "Проверить диск",
    "export_diagnostics": "Экспорт диагностики ZIP",
    "check_dependencies": "Проверить зависимости",
    "start_benchmark": "Запустить бенчмарк",
    "stop_benchmark": "Остановить",
    "console_commands": "Консольные команды",
    "console_commands_desc": "Отправка команд в игру через DLL hook. Требуется активная DLL инъекция.",
    "send": "Отправить",
    "launch_param_builder": "Конструктор параметров запуска",
    "launch_param_builder_desc": "Визуальный конструктор флагов Unity/Steam. Отмечайте галочки и командная строка собирается автоматически.",
    "show_advanced": "Показать продвинутое",
    "hide_advanced": "Скрыть продвинутое",
    "advanced_system_tweaks": "Продвинутые системные твики",
    "advanced_system_tweaks_desc": "Эти настройки изменяют Windows. Могут помочь FPS/пингу, но могут повлиять на другие приложения. Все обратимы.",
    "safe_tweaks_desc": "Эти твики безопасны и обратимы. Помогают Windows выделить больше ресурсов игре.",
    "windows_game_tweaks_desc": "Встроенные функции Windows для лучшей игровой производительности.",
    "discord_connected": "Подключено",
    "discord_status": "Статус",
    "discord_account": "Аккаунт Discord",
    "steam_link": "Привязка Steam",
}

# Overlay Launch Options keys
EN.update(LAUNCH_EN)
RU.update(LAUNCH_RU)
PL.update(LAUNCH_PL)

# Cleanup keys
CLEANUP_EN = {
    "cleanup_shader_cache": "Unity Shader Cache", "cleanup_shader_cache_desc": "Compiled shader cache. Safe to delete — recreated on launch.",
    "cleanup_crash_logs": "Crash Logs", "cleanup_crash_logs_desc": "crash.dmp and output_log.txt from previous crashes.",
    "cleanup_temp_updates": "Update Temp Files", "cleanup_temp_updates_desc": "Leftovers from interrupted Steam/game updates.",
    "cleanup_unity_logs": "Unity Logs", "cleanup_unity_logs_desc": "Player.log and old session logs.",
    "cleanup_workshop_temp": "Workshop Temp Files", "cleanup_workshop_temp_desc": "Incomplete mod downloads from Steam Workshop.",
}
CLEANUP_RU = {
    "cleanup_shader_cache": "Шейдерный кэш Unity", "cleanup_shader_cache_desc": "Кэш скомпилированных шейдеров. Безопасно удалять — пересоздаётся при запуске.",
    "cleanup_crash_logs": "Логи крашей", "cleanup_crash_logs_desc": "Файлы crash.dmp и output_log.txt от предыдущих падений.",
    "cleanup_temp_updates": "Временные файлы обновлений", "cleanup_temp_updates_desc": "Остатки от прерванных обновлений Steam/игры.",
    "cleanup_unity_logs": "Логи Unity", "cleanup_unity_logs_desc": "Player.log и старые логи сессий.",
    "cleanup_workshop_temp": "Временные файлы Workshop", "cleanup_workshop_temp_desc": "Незавершённые загрузки модов из Steam Workshop.",
}
CLEANUP_PL = {
    "cleanup_shader_cache": "Cache shaderów Unity", "cleanup_shader_cache_desc": "Skompilowany cache shaderów. Bezpieczne do usunięcia — odtworzony przy uruchomieniu.",
    "cleanup_crash_logs": "Logi crashów", "cleanup_crash_logs_desc": "Pliki crash.dmp i output_log.txt z poprzednich awarii.",
    "cleanup_temp_updates": "Tymczasowe pliki aktualizacji", "cleanup_temp_updates_desc": "Pozostałości po przerwanych aktualizacjach Steam/gry.",
    "cleanup_unity_logs": "Logi Unity", "cleanup_unity_logs_desc": "Player.log i stare logi sesji.",
    "cleanup_workshop_temp": "Tymczasowe pliki Workshop", "cleanup_workshop_temp_desc": "Nieukończone pobierania modów ze Steam Workshop.",
}
EN.update(CLEANUP_EN)
RU.update(CLEANUP_RU)
PL.update(CLEANUP_PL)

# Update keys
UPDATE_EN = {"update_available": "Update available:", "update_now": "Update Now", "update_later": "Later"}
UPDATE_RU = {"update_available": "Доступно обновление:", "update_now": "Обновить", "update_later": "Позже"}
UPDATE_PL = {"update_available": "Dostepna aktualizacja:", "update_now": "Aktualizuj", "update_later": "Pozniej"}
EN.update(UPDATE_EN)
RU.update(UPDATE_RU)
PL.update(UPDATE_PL)

# Build output
lines = []
lines.append('(function() {')
lines.append('  const LOCALE_DATA = {')

for lang, data in [('en', EN), ('ru', RU), ('pl', PL)]:
    lines.append(f'  "{lang}": {{')
    for key, val in sorted(data.items()):
        val = val.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n')
        lines.append(f'    "{key}": "{val}",')
    if lines[-1].endswith(','):
        lines[-1] = lines[-1][:-1]
    lines.append('  },')

if lines[-1].endswith(','):
    lines[-1] = lines[-1][:-1]

lines.append('  };')
lines.append('')
lines.append('  const I18N = {')
lines.append("    current: 'en',")
lines.append('    data: LOCALE_DATA,')
lines.append('    loaded: true,')
lines.append('')
lines.append('    init(defaultLang) {')
lines.append('      let lang = null;')
lines.append("      try { lang = localStorage.getItem('scptweaker_lang'); } catch(e) {}")
lines.append('      if (!lang && defaultLang) lang = defaultLang;')
lines.append("      if (!lang || !this.data[lang]) lang = 'en';")
lines.append('      this.current = lang;')
lines.append('      this.apply();')
lines.append('    },')
lines.append('')
lines.append('    t(key) {')
lines.append('      const dict = this.data[this.current];')
lines.append('      if (!dict) return key;')
lines.append("      return dict[key] || (this.data.en ? this.data.en[key] : key) || key;")
lines.append('    },')
lines.append('')
lines.append('    setLang(lang) {')
lines.append("      console.log('i18n setLang:', lang, 'has:', !!this.data[lang]);")
lines.append('      if (!this.data[lang]) return;')
lines.append('      this.current = lang;')
lines.append("      try { localStorage.setItem('scptweaker_lang', lang); } catch(e) {}")
lines.append('      this.apply();')
lines.append('      if (this._onLangChange) { try { this._onLangChange(lang); } catch(e) {} }')
lines.append('    },')
lines.append('')
lines.append('    apply() {')
lines.append("      console.log('i18n apply, lang:', this.current);")
lines.append("      const count = document.querySelectorAll('[data-i18n]').length;")
lines.append("      console.log('i18n elements:', count);")
lines.append('      let updated = 0;')
lines.append("      document.querySelectorAll('[data-i18n]').forEach(el => {")
lines.append('        const key = el.dataset.i18n;')
lines.append('        const text = this.t(key);')
lines.append('        if (text !== key) {')
lines.append("          if (el.tagName === 'INPUT' && el.type === 'text') {")
lines.append('            el.placeholder = text;')
lines.append("          } else if (el.tagName === 'TEXTAREA') {")
lines.append('            el.placeholder = text;')
lines.append("          } else if (el.tagName === 'INPUT' && (el.type === 'checkbox' || el.type === 'switch')) {")
lines.append('            const label = el.nextElementSibling;')
lines.append("            if (label && label.dataset.i18nLabel) {")
lines.append('              label.textContent = this.t(label.dataset.i18nLabel);')
lines.append('            }')
lines.append('          } else {')
lines.append('            el.textContent = text;')
lines.append('          }')
lines.append('          updated++;')
lines.append('        }')
lines.append('      });')
lines.append("      document.querySelectorAll('[data-i18n-label]').forEach(el => {")
lines.append('        const key = el.dataset.i18nLabel;')
lines.append('        const text = this.t(key);')
lines.append('        if (text !== key) { el.textContent = text; updated++; }')
lines.append('      });')
lines.append("      document.querySelectorAll('[data-i18n-title]').forEach(el => {")
lines.append('        const key = el.dataset.i18nTitle;')
lines.append('        const text = this.t(key);')
lines.append('        if (text !== key) { el.title = text; updated++; }')
lines.append('      });')
lines.append("      document.querySelectorAll('.lang-btn').forEach(btn => {")
lines.append('        btn.classList.toggle(\'active\', btn.dataset.lang === this.current);')
lines.append('      });')
lines.append("      console.log('i18n updated', updated, 'elements');")
lines.append('    }')
lines.append('  };')
lines.append('')
lines.append('  window.I18N = I18N;')
lines.append('})();')

with open('wwwroot/js/i18n.js', 'w', encoding='utf-8') as f:
    f.write('\n'.join(lines))

print(f'Written i18n.js with EN:{len(EN)} RU:{len(RU)} PL:{len(PL)} keys')
