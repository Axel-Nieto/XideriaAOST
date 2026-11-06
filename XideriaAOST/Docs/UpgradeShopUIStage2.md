# Tienda de mejoras: interfaz, etapa 2

## Alcance y funcionamiento

La interfaz ya está instalada en `Assets/Scenes/Zone -1/Lobby.unity`, como instancia de `Assets/Prefabs/UpgradeShop.prefab` bajo el Canvas GameplayHUD. Se creó `Assets/Prefabs/UpgradeShopItem.prefab` y se conectó `Assets/ScriptableObjects/Upgrades/VitalImpulse.asset`, con costo de prueba de 3 fragmentos. No hace falta repetir el montaje manual en el Lobby. Las instrucciones siguientes sirven como referencia para nuevos mapas; los prefabs creados son la configuración actual.

La sección Fuego incorpora `FireballCooldownUpgrade.asset` y `FlameDashCooldownUpgrade.asset`, ambas en `Assets/ScriptableObjects/Upgrades`, a **20 de oro cada una** y con el icono `Assets/Images/SkillIcons/Burn.png`. El encabezado de Fuego muestra el saldo de oro; la fila muestra el icono de moneda y el estado Oro insuficiente cuando corresponde. Sir Tete conserva Impulso Vital y su precio en fragmentos.

Sir Tete agrega cuatro mejoras a **20 fragmentos cada una**, con el mismo icono de Impulso Vital (`Energy 1.png`). Se compran una vez por run y conservan su efecto entre salas:

| Mejora | Asset en `Assets/ScriptableObjects/Upgrades` | Efecto |
| --- | --- | --- |
| Vitalidad Reforzada | `TeteMaxHealthUpgrade.asset` | +2 de vida máxima. Comprar no cura la vida actual. |
| Piel de Acero | `TeteDefenseUpgrade.asset` | +1 de defensa base. |
| Golpe Decidido | `TeteDamageUpgrade.asset` | +2 de daño base. |
| Cascos Reforzados | `TeteHelmetDurabilityUpgrade.asset` | +1 carga al equipar un casco desde el inventario. No repara el casco actual. |

`CharacterStats` calcula estos bonos sobre las estadísticas originales, sin acumularlos al recibir eventos repetidos o cargar otra sala. `Character` se conecta a los cambios de compras y aplica los bonos antes de restaurar la vida guardada. Los enemigos mantienen sus estadísticas. Al cargar una sesión, la durabilidad conserva las cargas guardadas; sólo un equipamiento nuevo desde el inventario recibe la carga extra. `BattleHUD` actualiza el máximo de vida mostrado después de la compra.

La sección Sir Tete tiene cinco filas. El `ScrollRect` existente permite recorrerlas con rueda, arrastre o barra vertical. La barra usa AutoHide y queda a la derecha del viewport sin reducir el ancho de las filas. Abrir el panel o cambiar de sección detiene el movimiento y vuelve al inicio; comprar conserva la posición. El viewport mantiene `RectMask2D`, y Fuego conserva sus dos ofertas.

Bola de Fuego reduce en 1 turno la espera de ese uso si impacta a dos o más enemigos distintos; un enemigo 2x2 cuenta una sola vez. Dash Llameante reduce en 1 turno la espera de ese uso si completa el desplazamiento sin impactar enemigos. Ambas mantienen su cooldown base de 2, su nombre y sus costos de energía. La reducción sólo se aplica al jugador después de comprar la mejora. Los tooltips agregan la condición al adquirirla y dejan de mostrarla al abandonar la run. Las compras persisten entre escenas y cambios de casco, con el mismo alcance de persistencia que Impulso Vital.

El panel comienza cerrado: `Canvas/UpgradeShop/PanelRoot` está inactivo. La ventana central está bajo `PanelRoot/Border/Window`, con fondo oscuro, borde y botones anaranjados, fuente TeteTipografi y sprites existentes. El Content está vacío en el asset: el script genera la fila al llamar `Open()`. La conexión posterior ya permite abrirlo al terminar el movimiento en Shop del Lobby `(6, 1)`; consultar [UpgradeShopConnectionStage3.md](UpgradeShopConnectionStage3.md). El procedimiento temporal de esta guía solo es necesario para probar la UI aislada de esa interacción.

`UpgradeShopUI.Open()`, `Close()` y `Refresh()` son los puntos de entrada. El controlador comienza cerrado y vive fuera del objeto que oculta. Al abrir se conecta a `InstanceChanged`, `BalanceChanged` y `UpgradesChanged`; al cerrar, deshabilitarse o destruirse elimina sus suscripciones. Si aún no hay sesión muestra cero y deshabilita las compras; al aparecer una sesión, se conecta y lee su estado actual. No hay `Update`, búsquedas de objetos ni saldos propios.

Cada elemento recibe su definición y un callback de compra. El panel mantiene una vista por ID estable; las aperturas y los refrescos la reutilizan. Las entradas nulas, inválidas, de tipos no implementados y los IDs repetidos se filtran. Si varias definiciones válidas comparten ID, se utiliza la primera de la lista. `UpgradeDefinition.IsSupported` valida los pares ID/tipo de Impulso Vital, las cuatro mejoras de Sir Tete y las dos mejoras de fuego; `Currency` y `Cost` indican la moneda y el importe correspondientes. Los IDs serializados existentes y el campo `fragmentCost` de Impulso Vital se conservan.

El elemento consulta `HasUpgrade` y el saldo de la moneda requerida para presentar Comprar, Fragmentos insuficientes, Oro insuficiente o Adquirida. Esas condiciones solo controlan la presentación; la compra real llama exclusivamente a `PlayerSessionData.TryPurchaseUpgrade`. El panel bloquea solicitudes reentrantes durante la llamada y vuelve a leer el estado al finalizar, incluso ante un rechazo. La sesión registra la compra, descuenta únicamente la moneda correspondiente y emite sus eventos después de confirmar la transacción.

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

ScrollView/ScrollRect: Content = Content, Viewport = Viewport, Horizontal desactivado, Vertical activado, Movement Type Clamped. El prefab actual agrega VerticalScrollbar con Handle, visibilidad AutoHide y sensibilidad 48. La barra es hermana del Viewport, anclada al borde derecho del ScrollView. Content/VerticalLayoutGroup: Padding 8 en los cuatro lados, Spacing 18 en el prefab actual, Child Alignment Upper Center, Control Child Size Width y Height activados, Child Force Expand Width activado y Height desactivado. ContentSizeFitter: Horizontal Fit Unconstrained, Vertical Fit Preferred Size. No agregar LayoutGroup al Viewport.

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

Los prefabs conectan `Assets/Images/Pedido/FragmentoXideria.png` y `Assets/Images/Pedido/Moneda.png` a sus campos de sprites de moneda. FragmentsBalance/Icon y Cost/Icon se actualizan según la sección y la definición, respectivamente; Preserve Aspect está activado y Raycast Target desactivado. El Icon principal de cada fila se asigna desde la definición al refrescar.

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
| Upgrades | Impulso Vital, TeteMaxHealthUpgrade, TeteDefenseUpgrade, TeteDamageUpgrade, TeteHelmetDurabilityUpgrade, FireballCooldownUpgrade y FlameDashCooldownUpgrade, una sola vez cada una |

El controlador necesita Panel Root, Items Container y Item Prefab para abrir. Si faltan, permanece cerrado y registra una advertencia. Los campos visuales ausentes no producen NullReferenceException, aunque se debe completar la configuración para obtener la presentación esperada. Un icono de definición ausente se oculta.

## Prueba manual sin controles de depuración permanentes

1. Entrar en Play Mode en el Lobby, con el personaje en reposo. La tienda debe comenzar cerrada.
2. **Durante Play Mode**, crear temporalmente un Button UI bajo el Canvas, fuera de UpgradeShop/PanelRoot. Nombrarlo `Abrir tienda (prueba)`, ubicarlo en un lugar libre y agregar a su OnClick la referencia a UpgradeShop → UpgradeShopUI.Open(). No guardar ese botón en la escena ni copiar los cambios de Play Mode a Edit Mode: desaparece al detener la ejecución.
3. Abrir y cerrar varias veces mediante ese botón y la X. Debe existir una sola fila de Impulso Vital y no debe ejecutarse ninguna compra al abrir/cerrar.
4. Con los fondos iniciales de 10 y un costo de prueba de 3, comprobar saldo 10 y botón Comprar. Comprar: saldo 7, estado Adquirida y botón deshabilitado; el oro sigue en 10. Repetir clics no vuelve a cobrar.
5. Para los casos de saldo insuficiente, llegada tardía de sesión, abandono y compra desde otro consumidor, ejecutar los tests de Editor o invocar las APIs existentes desde el depurador. No hace falta añadir controles permanentes para cambiar monedas.
6. Comprobar la disposición en Play Mode con Game View 1920×1080, 1280×720 y 4:3. Mantener anclajes y Canvas Scaler; revisar que el texto largo del botón y la descripción no se recorten. Recorrer las cinco filas de Sir Tete por rueda, arrastre y barra; comprobar la última fila completa y la ocultación de la barra en Fuego. Unity muestra las barras AutoHide en Edit Mode aunque el contenido entre completo; comprobar la ocultación durante Play Mode.
7. Reunir al menos 20 de oro, abrir Fuego y comprar una mejora: debe descontar 20 de oro sin gastar fragmentos. Verificar el texto y la condición en combate con el casco de fuego. Para Bola de Fuego, comparar un enemigo 2x2 con dos enemigos distintos. Para Dash Llameante, comparar un escape sin impactos con una llegada adyacente a un enemigo. Comprobar que el CD base mostrado siga siendo 2 y que sólo la espera del uso que cumple la condición quede en 1.
8. Reunir 80 fragmentos y comprar las cuatro mejoras nuevas de Sir Tete. Verificar vida máxima +2 sin curación, defensa +1 y ataque +2. Equipar un casco fuera del Lobby y verificar una carga extra; consumir cargas, cambiar de sala y confirmar que no se repongan al restaurarlo. Abandonar la run elimina las compras y sus bonos.

## Conexión modal posterior

`HelmetInventoryUI` utiliza `GridInputHandler.CanOpenHelmetInventory()` y `SetHelmetInventoryOpen(bool)` para bloquear sus propios controles, limpiar previsualizaciones y restaurarlas al cerrar. Esa señal está ligada al inventario y no debe reutilizarse como si la tienda fuera el inventario.

El fondo con Raycast Target por sí solo bloquea los clics de otros elementos de Unity UI, pero no las acciones que consultan directamente teclado/ratón. La conexión posterior mediante `UpgradeShopInteraction` y `GridInputHandler` ya bloquea el tablero, las habilidades y el fin de turno mientras se compra, impide superponer el inventario y protege el fotograma del clic de cierre. La apertura espera al final de la acción sobre Shop. No se reasignó Escape, no se modificó MainMenuUI y no se pausa el tiempo global. Ver la guía de conexión para los detalles y pruebas pendientes.

## Validación y límites

Para las cuatro mejoras de Sir Tete se compiló el código y se ejecutaron **168 comprobaciones sin fallos** en Unity 6000.3.9f1, en un proyecto aislado bajo `Library/CodexFireUpgradesProject`. `TeteUpgradeTests.cs` cubre compras, aplicación inmediata, HUD, persistencia sin acumulación, sesiones tardías, enemigos 1x1/2x2, daño real, durabilidad de Fuego/Agua/Tierra y restricciones del Lobby. Se repitieron las regresiones de tienda, run, fuego y recompensas. La comprobación nativa del prefab preparado importó las cuatro definiciones y verificó precios, iconos, cinco filas, rueda, arrastre, barra, última compra y reinicio entre secciones. Los archivos aplicados coinciden byte a byte con los preparados; se preservó el `.meta` del prefab. La apariencia y la ocultación automática de la barra en el Lobby real quedan pendientes de Play Mode; la comprobación de Editor validó los tamaños del contenido y la política AutoHide.

Se agregaron tests `UpgradeShopUITests` para cierre inicial, apertura/cierre, saldo y datos de definición, estados suficiente/insuficiente/adquirida, clics rápidos y reentrantes, compras externas, abandono, rechazo con presentación desactualizada, reaperturas, llegada tardía/pérdida de sesión, deshabilitación/destrucción/recreación, lista con múltiples entradas y deduplicación, configuraciones inválidas y referencias faltantes.

La compilación pasó con los ensamblados de Unity 6000.3.9f1. Se ejecutaron 134 comprobaciones en un proyecto temporal de Unity, sin fallos: compras, estado de la run, Impulso Vital, tienda, recompensas, las mejoras de fuego y la importación de sus dos definiciones con precio de 20 de oro e icono Burn. Incluyen compras repetidas y reentrantes, saldo insuficiente, persistencia, textos, enemigos 1x1/2x2, dashes interrumpidos, exclusión de la IA y conservación del cooldown base entre usos. `FireUpgradeTests.cs` contiene los casos de compra y combate; `UpgradeShopUITests.cs` comprueba las dos ofertas de Fuego y el cambio de moneda. Estas comprobaciones no sustituyen la prueba visual en Game View.

Las conexiones de los dos prefabs se comprobaron mediante revisión de referencias y comparación de objetos serializados: sólo cambia su configuración de tienda, sin modificar su distribución. Se preservaron sus metadatos y los GUID existentes; las dos definiciones nuevas tienen metadatos propios. La apariencia en Game View y la compra desde la instancia real del Lobby quedan pendientes de comprobar en Play Mode.

La UI pertenece al Lobby y se abre mediante la interacción conectada a Shop. El guardado de compras continúa siendo parte del estado de la run en memoria; no se agregó guardado en disco.
