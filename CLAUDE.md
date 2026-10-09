# DruggingStuff (Сортировка дома)

Казуальная 3D-игра для Яндекс Игр: игрок перетаскивает предметы между колонками полок и собирает одинаковые, чтобы очистить шкаф. Кампания из 20 уровней на время в 4 главах по 5 уровней, бесконечный режим, обучение. Соло-проект Михаила, цель: выпустить в октябре 2026.

Общаемся на русском. Ответы короткие и по делу.

## Приоритет правил
1. Главный файл Михаила, общий для всей работы: @~/Documents/Claude/CLAUDE.md (`C:\Users\Михаил\Documents\Claude\CLAUDE.md`). Он главнее этого файла. При конфликте следовать ему и сказать о конфликте.
2. Этот файл: всё про проект DruggingStuff.
3. Частные правила в `.claude/rules/` подгружаются сами по маске путей:

| Файл | Когда действует | Что внутри |
|------|-----------------|------------|
| `.claude/rules/code-style.md` | любые `*.cs` | стиль кода, именование, устройство Unity-компонентов |

Новое правило заводить, когда агент повторяет одну и ту же ошибку, и сразу добавлять строку в эту таблицу.

## Документы
- `docs/ГДД_2026-09-30.docx`: ГДД для людей. Основа не меняется, новое дописывается в последний раздел «Модификации и изменения». Без путей, классов и устройства проекта, в стиле первой части (нумерация 1), a), е вместо ё, без длинных тире).
- `docs/GDD.md`: техническая сводка по реализации. При расхождениях главнее планы в `docs/plans/`.
- `docs/plans/`: планы реализации крупных фич (колонки, кампания, закрытые полки, конвейеры, полка-фильтр, главы кампании). Новую крупную фичу начинать с плана здесь же.
- `docs/2026-10-04-chapters-and-filter-shelf.md`: решения по главам кампании и полке-фильтру.
- `docs/ui-redesign-2026-09-20-handoff.md`: палитра и состояние UI.
- `Art/Blender/Conveyor/README.md`: модель конвейерной ленты.

## Стек
- Unity 2022.3.62f2, сборка WebGL, горизонтальный экран 16:9 (референс 1920x1080, Match = 1).
- PluginYG2 (`Assets/PluginYourGames`, пространство имён `YG`): реклама, лидерборды, авторизация, облачные сохранения, оценка игры.
- uGUI + TextMeshPro, DOTween (`Assets/Plugins/Demigiant`).
- Unity MCP: пакет `com.coplaydev.unity-mcp` (MCP for Unity), транспорт stdio. Сервер `UnityMCP` включён в `.claude/settings.local.json`.
- Git + Git LFS, удалённый репозиторий github.com/jacket0/DraggingStuff.
- Ввод: старый Input Manager (`Input.touches`, мышь) + EventSystem для проверки UI. Локализация (RU, EN, TR) через YG2 (`YG2.lang`, `YG2.onSwitchLang`, `LanguagePreference`). Анимации: DOTween; тайминги: корутины.
- Настройки и данные: ScriptableObject (`LevelCatalog`, `BonusDefinition`, `TimedLevelDefinition` и т.д.). Зависимости между компонентами: ссылки в инспекторе.

Не используется, без обсуждения не добавлять: DI-фреймворки (Zenject, VContainer), UniTask и `async`/`await`, Addressables, новый Input System, пакет Unity Localization, URP (проект на встроенном рендере).

## Структура
Сцены (`Assets/Scenes`): `Startup` → `TutorialLevel` или `MainMenu`; главы кампании: `SimpleLevel` (глава 1, уровни 1-5), `ThirdLevel` (глава 2, 6-10, полки-фильтры), `FourthLevel` (глава 3, 11-15, закрытые полки), `FifthLevel` (глава 4, 16-20, конвейеры); `EndlessLevel`. `SecondLevel` (полки на 4 колонки) в кампанию не входит, её старые уровни лежат в `Assets/Levels/Archive`.

Как запускается игра:
1. `Startup`: `GameStartup` загружает `TutorialLevel`, если обучение не пройдено, иначе `MainMenu`.
2. `MainMenu`: выбор уровня. Доступность считает `LevelProgressService`, выбранный уровень передаётся в сцену главы через ScriptableObject `LevelSelectionState`.
3. Сцена уровня: сессия (`LevelSession`, `EndlessSession`, `TutorialSession`, все наследники `GameSession`) ведёт состояние попытки. Механики главы (`ClosedShelvesController`, `ConveyorController`, `ShelfFiltersController`) лежат на сцене, подписываются на события сессии и получают зависимости ссылками в инспекторе.
4. Прогресс: статический `GameProgressRepository` поверх сохранений YG2, локальное и облачное сохранения сливаются.
Новый сервис уровня добавлять так же: компонент на сцене главы, ссылки в инспекторе, подписка на события `GameSession`/`LevelSession`.

Данные уровней: `Assets/Levels/Menu` (`MainLevelCatalog`, главы `Chapters/LevelChapter_0N`, записи `LevelEntry_CN_0M`), `Assets/Levels/Timed` (определения `TimedLevel_CN_0M`).

Код (`Assets/Scripts`, без asmdef и без namespace):
- `Model/`: доска, полки, колонки, предметы (компоненты сцены), снимки состояния и симулятор ходов (чистый C# без Unity).
- `Level/`: сессии (`GameSession` и наследники), таймеры, звёзды, прогресс, сборка уровней из вариантов; `Level/Config/`: данные уровней и валидатор.
- `Bonus/`: инвентарь, эффекты (подсказка, заморозка комбо), rewarded-выдача. `Combo/`, `Score/`, `Modifiers/`.
- `ClosedShelves/`: условия открытия, пополнение тройками. `Conveyor/`: сдвиг лент. Полка-фильтр: данные `ShelfFilterDefinition`, отклик и таблички в `View/ShelfFilters`.
- `Endless/`: генерация, пул предметов, пополнение.
- `Input/`: перетаскивание и выбор цели. `View/`: всё отображение, HUD, анимации, окна.
- `Platform/Yandex/`: реализации интерфейсов платформы (`IInterstitialAdService`, `IRewardedAdService`, `IGameReviewService` и др.). `Progress/`: сохранения и слияние локального и облачного прогресса.

Редактор (`Assets/Editor`):
- `TimedLevels/`: генератор раскладок, солвер, валидации, базовая линия генератора (`Regression/generator-baseline.json`), сборка WebGL.
- `ClosedShelves/`: бот баланса и настройка визуала закрытых полок.

## Ключевые решения
- Полка состоит из независимых колонок (`ShelfColumn`), предметы в колонке стоят очередью спереди назад. Послойной модели больше нет, не возвращать её.
- Правила хода живут в `ShelfBoard` и `BoardMoveSimulator`. Подсказка, генератор и солвер работают через симулятор, поэтому любое изменение правил должно одинаково попасть и в доску, и в симулятор.
- Раскладки кампании генерируются и проверяются солвером заранее, в рантайме солвер не используется. На уровень хранится не меньше 12 подтверждённых вариантов в `TimedLevelDefinition`. Изменил правила или уровни: перегенерировать варианты и прогнать проверки.
- Чистая логика хода (`BoardMoveSimulator`, `*StateSnapshot`) не зависит от Unity и отображения, её стоит такой и держать. Компоненты сцены (`Shelf`, `ShelfBoard`, `GameSession`) связаны со своими view, это принятое решение, переделывать его без запроса не нужно.
- Кампания хранится главами: `LevelCatalog` → `LevelChapter` → `LevelEntry`. Номер уровня равен позиции в каталоге и одновременно ключ прогресса и основа сидов генератора: перестановка уровней требует миграции сохранений (`CampaignProgressMigration`, версия `GameProgressData`) и перезаписи эталона генератора. Уровень открыт, если пройдены все предыдущие.
- Платформа скрыта за интерфейсами, прямые вызовы `YG2` только в `Platform/Yandex` и местах, где это уже принято.
- PluginYG2 изменён локально: в `Assets/PluginYourGames/Modules/Storage/Scripts/Storage_yg.cs`, когда есть и облачное, и локальное сохранение, вместо выбора по `idSave` вызывается `GameProgressSaveConflictResolver.Resolve`. После обновления плагина вернуть этот вызов, иначе облачный и локальный прогресс перестанут сливаться.
- UI собирается вручную в редакторе, геометрия канвасов сериализована в сценах и префабах. Не вычислять layout в рантайме и не генерировать UI кодом.

## Правила кода
Вынесены в `.claude/rules/code-style.md`, подгружаются автоматически при работе с `*.cs`. Коротко: просто и понятно, без абстракций ради принципов, понятные имена, без комментариев, чинить причину.

## Эталоны
Новое делать по образцу этих решений: смотреть, как они устроены, и повторять структуру, имена и способ подключения.

| Что делаем | Эталон | На что смотреть |
|------------|--------|-----------------|
| Новая механика уровня | Полка-фильтр и коробки: `ShelfFilters/`, `View/ShelfFilters/`, проверки `Editor/TimedLevels/ShelfFilter*`, планы `docs/plans/2026-10-04-shelf-filter-implementation.md` и `2026-10-07-filter-box-rule-implementation.md`. Второй образец: конвейеры (`Conveyor/`, `View/Conveyor/`, план `2026-09-28-conveyors-implementation.md`) | Порядок этапов: план → правило в модели и симуляторе → генератор и данные уровней → контроллер на сцене → view → проверки в редакторе и сценарии Play Mode → базовая линия генератора → GDD.md, ГДД, CLAUDE.md |
| Контроллер механики на сцене | `ConveyorController` | Ссылки в инспекторе, проверки в `Awake`, подписки в `OnEnable`/`OnDisable`, события наружу |
| Новый бонус | `TimerFreezeBonusEffect` и `HintBonusEffect` (наследники `BonusEffect`) + ассет `BonusDefinition` в `BonusCatalog` | Эффект отдельно от данных; лимиты и перезарядка в `BonusDefinition` |
| Сервис платформы | `IInterstitialAdService` + `Platform/Yandex/Advertisement/YandexInterstitialAdService` | Интерфейс в общем коде, вызовы `YG2` только в реализации |
| Проверка в редакторе | `TimedCampaignValidation`; проверка модели механики `ConveyorModelValidation` | Пункт меню `Tools/Timed Levels/...`, исключение при ошибке, маркер `..._PASS` в консоли. Проверку модели новой механики оформлять отдельным классом со статическим `Run(LevelCatalog)` и вызывать из `Validate Gameplay Changes`, как `ConveyorModelValidation` |
| План крупной фичи | `docs/plans/2026-10-04-shelf-filter-implementation.md` | Этапы с проверкой на каждом, коммиты вида `Фича: этап N, что сделано` |

## Локальные CLAUDE.md
Пока нет. Для сложной системы с собственными правилами (кандидаты: генератор и солвер в `Assets/Editor/TimedLevels`, `ClosedShelves`) можно завести CLAUDE.md в её папке: он подгружается, только когда агент работает внутри. Заводить по мере необходимости, а не заранее.

## Как работать в проекте
- Порядок работы с агентом (план до кода, вопросы перед планом, одна задача на чат) описан в главном файле, раздел "Работа с ИИ-агентами".
- Перед изменениями смотреть `git status`: в рабочем дереве могут быть незакоммиченные правки Михаила. Не откатывать, не форматировать и не перезаписывать чужие изменения.
- Сцены, префабы и ScriptableObject менять через Unity Editor или Unity MCP, а не правкой YAML вручную. `.meta` не создавать и не править руками, их создаёт Unity.
- Не трогать: `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, сторонние пакеты и ассеты (`PluginYourGames`, `Plugins/Demigiant`, `TextMesh Pro`, `Packages/TextMesh Pro`, `PandazoleHome`, `JustPlay`, `AssetsStore`), `_Recovery`.
- Временные файлы класть в `.utmp/` (в `.gitignore`).
- Коммиты только по указанию Михаила. Соло-проект обычно ведётся без лишних веток; сейчас открыта ветка `feature/campaign-chapters`. Сообщения коммитов короткие, по-русски или в формате `feat:`/`fix:`/`docs:`/`chore:`.

## Проверки
Юнит-тестов нет, проверки запускаются из меню редактора `Tools/Timed Levels`:
- `Validate Campaign`: данные и варианты всех уровней кампании.
- `Validate Gameplay Changes`: симуляция ходов, закрытые полки, пополнение тройками, конвейеры, фильтры, данные уровней в сценах.
- `Validate Filter Chapter`, `Validate Filter Play Mode`: уровни главы 2 и сценарии полки-фильтра в Play Mode.
- `Check Generator Baseline` / `Record Generator Baseline`: сравнение генератора с базовой линией. Записывать новую линию только если изменение генератора намеренное.
- `Validate Play Mode Route`, `Validate Conveyor Play Mode`: прогон уровней в Play Mode.
- `Closed Shelves/Run Balance Bot (Preview)` и полная версия: бот баланса закрытых полок.
- Сборка WebGL для проверки: `TimedCampaignWebGlBuild.Run` (development, в `.utmp/timed-campaign-webgl-final`). Архивы сборок для Яндекс Игр лежат в `Builds/`.

После изменений правил доски, генератора или данных уровней минимум: `Validate Gameplay Changes`, `Validate Campaign`, `Check Generator Baseline` и проверка в Play Mode затронутых уровней. Успешные проверки пишут в консоль маркер вида `..._PASS`.

## Словарь (игра → код)
| Термин | В коде |
|--------|--------|
| Доска | `ShelfBoard` |
| Полка / колонка / предмет | `Shelf` / `ShelfColumn` / `ShelfItem`, тип `ItemType` |
| Мэтч | `MatchResolution` |
| Вариант раскладки | `TimedLevelVariant` |
| Закрытая полка | `ClosedShelvesController`, условия в `ClosedShelves/Conditions` |
| Конвейер | `ConveyorController`, `ConveyorBeltView` |
| Бесконечный уровень | `EndlessSession` |
| Глава кампании | `LevelChapter`, в меню `LevelChapterSectionView` |
| Полка-фильтр | `ShelfFilterDefinition`, `ShelfFiltersController` |
