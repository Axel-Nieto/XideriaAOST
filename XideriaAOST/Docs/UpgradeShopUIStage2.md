# Tienda de mejoras: interfaz, etapa 2

## Alcance y funcionamiento

La interfaz ya está instalada en `Assets/Scenes/Zone -1/Lobby.unity`, como instancia de `Assets/Prefabs/UpgradeShop.prefab` bajo el Canvas GameplayHUD. Se creó `Assets/Prefabs/UpgradeShopItem.prefab` y se conectó `Assets/ScriptableObjects/Upgrades/VitalImpulse.asset`, con costo de prueba de 3 fragmentos. No hace falta repetir el montaje manual en el Lobby. Las instrucciones siguientes sirven como referencia para nuevos mapas; los prefabs creados son la configuración actual.

El panel comienza cerrado: `Canvas/UpgradeShop/PanelRoot` está inactivo. La ventana central está bajo `PanelRoot/Border/Window`, con fondo oscuro, borde y botones anaranjados, fuente TeteTipografi y sprites existentes. El Content está vacío en el asset: el script genera la fila al llamar `Open()`. La conexión posterior ya permite abrirlo al terminar el movimiento en Shop del Lobby `(6, 1)`; consultar [UpgradeShopConnectionStage3.md](UpgradeShopConnectionStage3.md). El procedimiento temporal de esta guía solo es necesario para probar la UI aislada de esa interacción.

`UpgradeShopUI.Open()`, `Close()` y `Refresh()` son los puntos de entrada. El controlador comienza cerrado y vive fuera del objeto que oculta. Al abrir se conecta a `InstanceChanged`, `BalanceChanged` y `UpgradesChanged`; al cerrar, deshabilitarse o destruirse elimina sus suscripciones. Si aún no hay sesión muestra cero y deshabilita las compras; al aparecer una sesión, se conecta y lee su estado actual. No hay `Update`, búsquedas de objetos ni saldos propios.

Cada elemento recibe su definición y un callback de compra. El panel mantiene una vista por ID estable; las aperturas y los refrescos la reutilizan. Las entradas nulas, inválidas, de tipos no implementados y los IDs repetidos se filtran. Si varias definiciones válidas comparten ID, se utiliza la primera de la lista. La lista y el prefab de elemento permiten ampliar la presentación sin reescribir su estructura. Por ahora solo se acepta el ID/tipo de Impulso Vital; cuando se implemente otra mejora en el backend también deberá habilitarse en el filtro de presentación `UpgradeShopItemUI.CanDisplay`.

El elemento consulta `HasUpgrade` y el saldo de la sesión para presentar Comprar, Fragmentos insuficientes o Adquirida. Esas condiciones solo controlan la presentación; la compra real llama exclusivamente a `PlayerSessionData.TryPurchaseUpgrade`. El panel bloquea solicitudes reentrantes durante la llamada y vuelve a leer el estado al finalizar, incluso ante un rechazo. No descuenta monedas, no registra compras y no activa pasivas por su cuenta.

## Canvas y jerarquía manual

Usar el `Canvas` existente con tag `GameplayHUD` del Lobby, como hermano de `BattleHUD` y `CurrencyHUD`. Conservar su `Canvas Scaler` existente: Scale With Screen Size, referencia 1920 × 1080, Match 0.5. Mantener el `Graphic Raycaster` y el EventSystem existentes; no agregar otro Canvas ni otro sistema de input.

Crear esta jerarquía. El componente `UpgradeShopUI` va en **UpgradeShop**, nunca en PanelRoot ni en un hijo de PanelRoot:

```text
Canvas [GameplayHUD]
├── BattleHUD                    (existente)
├── CurrencyHUD                  (existente)
└── UpgradeShop                  [UpgradeShopUI]
    └── PanelRoot                [Image: fondo semitransparente]
        └── Window               [Image]
            ├── Title            [TextMeshProUGUI]
            ├── CloseButton      [Image, Button]
            │   └── Label        [TextMeshProUGUI]
            ├── FragmentsBalance
            │   ├── Icon         [Image]
            │   └── Amount       [TextMeshProUGUI]
            └── ScrollView       [ScrollRect]
                └── Viewport     [RectMask2D]
                    └── Content  [VerticalLayoutGroup, ContentSizeFitter]
```

Crear `UpgradeShop` como último hijo del Canvas para que aparezca sobre el HUD. No agregarle Image. Dejarlo **activo** y dejar PanelRoot **inactivo** al guardar. El script también oculta PanelRoot en Awake.

Configuración de RectTransform, con escala local (1,1,1) y rotación cero:

| Objeto | Anchors min / max | Pivot | Posición y tamaño / offsets |
| --- | --- | --- | --- |
| UpgradeShop | (0,0) / (1,1) | (0.5,0.5) | Left, Right, Top, Bottom: 0 |
| PanelRoot | (0,0) / (1,1) | (0.5,0.5) | Left, Right, Top, Bottom: 0 |
| Window | (0.5,0.5) / (0.5,0.5) | (0.5,0.5) | Pos (0,0), Width 1040, Height 680 |
| Title | (0,1) / (0,1) | (0,1) | Pos (32,-24), Width 820, Height 52 |
| CloseButton | (1,1) / (1,1) | (1,1) | Pos (-24,-24), Width 48, Height 48 |
| FragmentsBalance | (1,1) / (1,1) | (1,1) | Pos (-32,-94), Width 330, Height 44 |
| FragmentsBalance/Icon | (0,0.5) / (0,0.5) | (0,0.5) | Pos (0,0), Width 40, Height 40 |
| FragmentsBalance/Amount | (0,0) / (1,1) | (0.5,0.5) | Left 52, Right 0, Top 0, Bottom 0 |
| ScrollView | (0,0) / (1,1) | (0.5,0.5) | Left 24, Right 24, Top 156, Bottom 24 |
| Viewport | (0,0) / (1,1) | (0.5,0.5) | Todos los offsets 0 |
| Content | (0,1) / (1,1) | (0.5,1) | Pos (0,0), Size Delta X 0; la altura la calcula ContentSizeFitter |

PanelRoot/Image: color negro con Alpha 0.65 y Raycast Target activado. Window/Image: usar un sprite de panel existente o la imagen UI predeterminada, con un color oscuro legible. No hace falta crear imágenes nuevas. Title: `Mejoras de Sir Tete`. CloseButton/Label: `X`, Stretch completo, offsets 0, Middle Center. No asignar eventos OnClick manuales al botón de cerrar: el script conecta y desconecta su listener.

ScrollView/ScrollRect: Content = Content, Viewport = Viewport, Horizontal desactivado, Vertical activado, Movement Type Clamped. No se necesitan barras para esta etapa. Content/VerticalLayoutGroup: Padding 8 en los cuatro lados, Spacing 12, Child Alignment Upper Center, Control Child Size Width y Height activados, Child Force Expand Width activado y Height desactivado. ContentSizeFitter: Horizontal Fit Unconstrained, Vertical Fit Preferred Size. No agregar LayoutGroup al Viewport.

## Elemento reutilizable

Crear manualmente un objeto UI `UpgradeShopItem` para convertirlo en un **prefab nuevo**, sin reemplazar los prefabs existentes. Agregar `Image`, `LayoutElement` y `UpgradeShopItemUI` en su raíz:

```text
UpgradeShopItem [Image, LayoutElement, UpgradeShopItemUI]
├── Icon        [Image]
├── Name        [TextMeshProUGUI]
├── Description [TextMeshProUGUI]
├── Cost
│   ├── Icon    [Image de fragmentos]
│   └── Amount  [TextMeshProUGUI]
└── PurchaseButton [Image, Button]
    └── Label      [TextMeshProUGUI]
```

RectTransform raíz: Width 960, Height 200, Pivot (0.5,0.5). LayoutElement: Preferred Height 200, Flexible Width 1. El contenedor controla el ancho. Image de fondo con color blanco Alpha 0.08, Raycast Target desactivado.

| Hijo | Anchors min = max | Pivot | Pos | Width × Height |
| --- | --- | --- | --- | --- |
| Icon | (0,0.5) | (0,0.5) | (16,0) | 104 × 104 |
| Name | (0,1) | (0,1) | (140,-16) | 550 × 36 |
| Description | (0,1) | (0,1) | (140,-58) | 550 × 110 |
| Cost | (1,1) | (1,1) | (-16,-20) | 232 × 36 |
| Cost/Icon | (0,0.5) | (0,0.5) | (0,0) | 32 × 32 |
| PurchaseButton | (1,0) | (1,0) | (-16,16) | 232 × 64 |

Cost/Amount: Stretch/Stretch, Left 44, Right 0, Top 0, Bottom 0. PurchaseButton/Label: Stretch/Stretch, todos los offsets 8, Middle Center, permitir dos líneas para `Fragmentos insuficientes`. Asignar PurchaseButton/Image como Target Graphic del Button. No asignar eventos OnClick manualmente: cada fila registra su propio listener.

Usar `Assets/Fonts/TeteTipografi SDF.asset` en los textos. Valores iniciales sugeridos: título 36, nombre 28, descripción 20, saldos y costo 24, botón Auto Size 14–22. Textos con Raycast Target desactivado, márgenes 0 y color claro; descripción con ajuste de línea. En saldos/costos habilitar Auto Size y reservar el ancho para números de hasta diez dígitos. Son tamaños visuales ajustables, sin impacto en el costo ni la lógica.

Asignar `Assets/Images/Pedido/FragmentoXideria.png` a FragmentsBalance/Icon y Cost/Icon; Preserve Aspect activado, Raycast Target desactivado. El Icon principal de cada fila se asigna desde la definición al refrescar; no hay que fijarle el icono de Impulso Vital en el prefab.

En `UpgradeShopItemUI`, conectar:

| Campo | Referencia |
| --- | --- |
| Upgrade Icon | Icon/Image |
| Name Text | Name/TextMeshProUGUI |
| Description Text | Description/TextMeshProUGUI |
| Cost Text | Cost/Amount/TextMeshProUGUI |
| Purchase Button | PurchaseButton/Button |
| Purchase Button Text | PurchaseButton/Label/TextMeshProUGUI |

El prefab `Assets/Prefabs/UpgradeShopItem.prefab` ya fue creado y conectado. Si se reproduce esta estructura manualmente para otra tienda, guardar el prefab nuevo y eliminar su instancia de construcción del Content para dejarlo vacío.

## Definición y conexiones del panel

La definición `Assets/ScriptableObjects/Upgrades/VitalImpulse.asset` ya está creada y conectada. Para configurar otra copia mediante Create > Xideria > Upgrade Definition, usar:

- Upgrade Id: `sir_tete.vital_impulse`.
- Display Name: `Impulso Vital`.
- Description: `Recupera hasta 2 puntos de energía cuando un enemigo termina su secuencia de muerte.`
- Icon: `Assets/Images/SkillIcons/Energy 1.png` (el recurso existe).
- Fragment Cost: un costo positivo para pruebas, por ejemplo 3; no representa balance definitivo.
- Upgrade Type: VitalImpulse.

En `UpgradeShopUI` conectar:

| Campo | Referencia |
| --- | --- |
| Panel Root | PanelRoot |
| Fragments Balance Text | FragmentsBalance/Amount/TextMeshProUGUI |
| Close Button | CloseButton/Button |
| Items Container | ScrollView/Viewport/Content |
| Item Prefab | Componente UpgradeShopItemUI del prefab nuevo |
| Upgrades | Lista con la definición de Impulso Vital una sola vez |

El controlador necesita Panel Root, Items Container y Item Prefab para abrir. Si faltan, permanece cerrado y registra una advertencia. Los campos visuales ausentes no producen NullReferenceException, aunque se debe completar la configuración para obtener la presentación esperada. Un icono de definición ausente se oculta.

## Prueba manual sin controles de depuración permanentes

1. Entrar en Play Mode en el Lobby, con el personaje en reposo. La tienda debe comenzar cerrada.
2. **Durante Play Mode**, crear temporalmente un Button UI bajo el Canvas, fuera de UpgradeShop/PanelRoot. Nombrarlo `Abrir tienda (prueba)`, ubicarlo en un lugar libre y agregar a su OnClick la referencia a UpgradeShop → UpgradeShopUI.Open(). No guardar ese botón en la escena ni copiar los cambios de Play Mode a Edit Mode: desaparece al detener la ejecución.
3. Abrir y cerrar varias veces mediante ese botón y la X. Debe existir una sola fila de Impulso Vital y no debe ejecutarse ninguna compra al abrir/cerrar.
4. Con los fondos iniciales de 10 y un costo de prueba de 3, comprobar saldo 10 y botón Comprar. Comprar: saldo 7, estado Adquirida y botón deshabilitado; el oro sigue en 10. Repetir clics no vuelve a cobrar.
5. Para los casos de saldo insuficiente, llegada tardía de sesión, abandono y compra desde otro consumidor, ejecutar los tests de Editor o invocar las APIs existentes desde el depurador. No hace falta añadir controles permanentes para cambiar monedas.
6. Detener Play Mode y comprobar la disposición con Game View 1920×1080, 1280×720 y 4:3. Mantener anclajes y Canvas Scaler; revisar que el texto largo del botón y la descripción no se recorten. La lista usa ScrollRect para futuras filas.

## Conexión modal posterior

`HelmetInventoryUI` utiliza `GridInputHandler.CanOpenHelmetInventory()` y `SetHelmetInventoryOpen(bool)` para bloquear sus propios controles, limpiar previsualizaciones y restaurarlas al cerrar. Esa señal está ligada al inventario y no debe reutilizarse como si la tienda fuera el inventario.

El fondo con Raycast Target por sí solo bloquea los clics de otros elementos de Unity UI, pero no las acciones que consultan directamente teclado/ratón. La conexión posterior mediante `UpgradeShopInteraction` y `GridInputHandler` ya bloquea el tablero, las habilidades y el fin de turno mientras se compra, impide superponer el inventario y protege el fotograma del clic de cierre. La apertura espera al final de la acción sobre Shop. No se reasignó Escape, no se modificó MainMenuUI y no se pausa el tiempo global. Ver la guía de conexión para los detalles y pruebas pendientes.

## Validación y límites

Se agregaron tests `UpgradeShopUITests` para cierre inicial, apertura/cierre, saldo y datos de definición, estados suficiente/insuficiente/adquirida, clics rápidos y reentrantes, compras externas, abandono, rechazo con presentación desactualizada, reaperturas, llegada tardía/pérdida de sesión, deshabilitación/destrucción/recreación, lista con múltiples entradas y deduplicación, configuraciones inválidas y referencias faltantes.

La compilación de ejecución, Editor y tests pasó con los ensamblados de Unity 6000.3.9f1. Se ejecutaron 66 casos administrados anteriores de monedas/recompensas/compras: todos pasaron, con cero fallos. Los 20 casos nuevos de interfaz están compilados, pero no ejecutados: utilizan objetos nativos de UI, TextMeshPro y una escena de previsualización descartable. Ejecutarlos en Unity Test Runner → EditMode → UpgradeShopUITests. La compilación no sustituye esa ejecución ni la validación visual en Game View. La comparación de hashes confirmó que los 1037 archivos previos de Assets permanecieron intactos y que los GUID nuevos son válidos y únicos.

La instalación visual se comprobó mediante parseo YAML y revisión de referencias locales/externas, GUID y jerarquía. El cambio del Lobby consiste únicamente en agregar una instancia y su referencia como hijo del Canvas; el resto de los objetos serializados se preservó. La importación nativa, la apariencia en Game View y la compra desde esta instancia quedan pendientes de comprobar en Unity.

No se implementaron nuevas mejoras, tienda de cascos ni guardado en disco. La UI pertenece al Lobby y ahora se abre mediante la interacción conectada a Shop.
