# Модели ставни и табло

Рольставня ячейки и табло отправлений для циклических полок главы 6 (SeventhLevel). Анимация ставни, цифры табло и материалы живут в Unity (`ShelfShutterView`, `SplitFlapDisplay`, `DepartureBoardSlotView`).

## Файлы

| Файл | Что это |
|---|---|
| `build_lockers.py` | Параметрический скрипт. Главный источник моделей, сразу экспортирует оба FBX |
| `Lockers.blend` | Сохранённый результат |
| `Assets/Models/CyclicShelves/LockerShutter.fbx` | Ставня: короб, шторка, стекло окна |
| `Assets/Models/CyclicShelves/DepartureBoard.fbx` | Табло: панель и лампа |

## Состав и размеры

- `ShutterHousing`: короб с крышками и направляющими, 1092 треугольника, ширина 1.06 м.
- `ShutterCurtain`: ламели с гофром, окно на 3 стекла со стойками, нижняя планка с ручкой. 1120 треугольников, материалы `ShutterCurtain` и `ShutterHandle`. Высота 0.38 м, начало координат на верхнем крае: ставня поднимается сдвигом вверх, шейдер `Custom/ShutterCurtain` отрезает всё выше низа короба.
- `ShutterGlass`: один квад стекла окна, 2 треугольника, шейдер `Custom/ShutterCurtainGlass`.
- `DepartureBoardPanel`: панель 3.4 x 0.22 м с рамками окон 0.292 x 0.18 м под цифры и кольцами ламп, 2276 треугольников, материалы `DepartureBoard`, `DepartureBezel`, `DepartureWindow`.
- `DepartureLamp`: купол лампы радиусом 0.029 м, 180 треугольников. Стоит в слоте на x +0.25 от центра цифр.
- Лицевая сторона смотрит в +Z Unity (к камере).

## Как обновить

1. Запустить скрипт из консоли: `blender.exe -b --factory-startup --python build_lockers.py`. Он пересоберёт обе модели, сохранит `Lockers.blend` и перезапишет FBX.
2. В Unity: `Tools/Cyclic Shelves/Setup Visuals`. Он заменит сетки в SeventhLevel, настроит материалы и перерисует атлас цифр `Assets/Sprites/CyclicShelves/SplitFlapDigits.png`.
