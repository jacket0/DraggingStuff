# Модель конвейерной ленты

Одна лента на одну колонку. На конвейерном ярусе FifthLevel стоят три экземпляра (`Assets/Prefabs/Conveyor/ConveyorLanes.prefab`, x = +0.42 / 0 / −0.42 в пространстве `ConveyorMount`). Размеры места — в `mount-passport.md`.

## Файлы

| Файл | Что это |
|---|---|
| `build_conveyor_belt.py` | Параметрический скрипт. Главный источник модели |
| `ConveyorBelt.blend` | Сохранённый результат (Git LFS). Коллекция `ConveyorReference` — доска яруса, габариты предметов и игровая камера. В экспорт не попадает |
| `ConveyorBelt.png` | Текстура пластин, копия — `Assets/Textures/Conveyor/ConveyorBelt.png` |
| `Assets/Models/Conveyor/ConveyorBelt.fbx` | Экспорт |

## Состав модели

| Объект | Назначение |
|---|---|
| `ConveyorBelt` | Корень. Начало координат — точка, на которой стоит передний предмет колонки (поверхность ленты) |
| `Belt` | Замкнутая петля гусеницы, 72 треугольника. UV: U поперёк ленты, V вдоль движения, 1 единица V = 0.1 м = одна пластина. На верхней ветви V растёт от переднего края к заднему |
| `Frame` | Статичный корпус внутри петли и 4 Г-образные ножки с «пятками» до доски, 204 треугольника, один материал `ConveyorFrame` (одна подсетка — меньше вызовов отрисовки) |

Всего 276 треугольников, 2 материала, без модификаторов, костей, анимаций, камер и ламп.

## Оси (проверено 29.09 тестовым экспортом)

Экспорт с `Apply Transform` (`bake_space_transform=True`), `Forward −Z`, `Up Y`; в Unity `Bake Axis Conversion` включён. Корень импортируется с единичной трансформацией.

| Blender | Unity (`ConveyorMount`) | Смысл |
|---|---|---|
| +X | +X | к левому краю экрана |
| +Y | +Z | вперёд, к камере |
| +Z | +Y | нормаль ленты |

Без `Apply Transform` у корня FBX остаётся поворот 90° по X, и при установке с единичной локальной трансформацией он теряется.

## Параметры для `ConveyorBeltView`

- `_beltStepUv = 2` — сдвиг очереди 0.2 м = 2 пластины. Смещение UV растёт, поэтому пластины верхней ветви едут к камере.

## Как пересобрать

1. Открыть Blender с включённым аддоном MCP (или вставить скрипт в Text Editor).
2. Выполнить скрипт, затем:
   ```python
   ns = {"__name__": "conveyor_build"}
   exec(open(r"<repo>/Art/Blender/Conveyor/build_conveyor_belt.py", encoding="utf-8").read(), ns)
   ns["build"]()
   ns["build_reference"]()
   ns["save_track_texture"](r"<repo>/Assets/Textures/Conveyor/ConveyorBelt.png")
   ns["save_track_texture"](r"<repo>/Art/Blender/Conveyor/ConveyorBelt.png")
   ns["export_fbx"](r"<repo>/Assets/Models/Conveyor/ConveyorBelt.fbx")
   ```
   При запуске как `__main__` выполняются только `build()` и `build_reference()`.
3. Сохранить `ConveyorBelt.blend`.

Импорт в Unity: `Scale Factor 1`, `Convert Units`, `Bake Axis Conversion`, без камер, ламп, blend shapes, анимации и коллайдеров; `Rig: None`; материалы переназначены на `Assets/Materials/Conveyor/ConveyorBelt.mat` и `ConveyorFrame.mat`. Текстура: `Wrap Mode = Repeat`, sRGB, max size 128.

## Ограничения места

Под передним краем ленты до доски стойки около 6 см, поэтому петля низкая (толщина гусеницы 16 мм, внутренний радиус 10 мм). Спереди нижняя ветвь проходит в 7.7 мм над доской. Ножки вертикальны в мире (в пространстве места наклонены на 12°), пятки стоят на верхней грани доски. Доска кончается на z места ≈ −0.477, поэтому задние ножки стоят на −0.44, а хвост ленты свисает за край доски. Замеры досок — в `mount-passport.md`, зазор `PLANK_CLEARANCE_AT_ORIGIN = 0.0715`.
