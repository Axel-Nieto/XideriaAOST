# Etapa 3: configurar el HUD de monedas en Unity

El HUD ya está instalado en **Tutorial, Lobby, Level01–Level10 y LevelBossZ1**, con una instancia de **Assets/Prefabs/CurrencyHUD.prefab** como hijo directo del Canvas de cada mapa. La integración visual se realizó después de recibir autorización para modificar esos mapas. Los sprites, textos y referencias están conectados; no hace falta crear los objetos manualmente.

Para ajustar todos los mapas, editar el prefab compartido: `Horizontal Layout Group > Spacing` modifica la separación de los contadores y `RectTransform > Anchored Position Y` ajusta el margen superior. Las instrucciones siguientes documentan la configuración existente y sirven para agregarlo a mapas futuros.

## Estado y saldos iniciales

`CurrencyHUD` presenta los saldos de `PlayerSessionData.RunState`. Se conecta a `BalanceChanged`, lee ambos saldos al habilitarse y elimina sus suscripciones al deshabilitarse o destruirse. `InstanceChanged` permite conectarse aunque la sesión aparezca después del HUD. No utiliza `Update`, búsquedas por frame ni un almacenamiento de saldos propio.

La primera sesión de una partida nueva empieza con **10 fragmentos y 10 de oro** para pruebas. Un marcador serializable evita repetir esa asignación. Los cambios de escena, muerte, victoria, regreso al Lobby y `ResetSession()` conservan los saldos. Una sesión duplicada no vuelve a otorgarlos. Un estado que ya contiene monedas o progreso tampoco recibe esta asignación inicial.

`AbandonRun()` sigue dejando ambos saldos en **cero**, y el HUD se actualiza mediante los eventos existentes. Volver a habilitarlo o cambiar de mapa después de abandonar no concede otros 10. No se ha cambiado el menú ni se ha añadido una operación de inicio de otra run.

## Canvas y jerarquía

En Lobby, Tutorial, Level01–Level10 y LevelBossZ1 existe un `Canvas` con el tag **GameplayHUD**, en modo **Screen Space - Overlay**. Su `Canvas Scaler` ya utiliza **Scale With Screen Size**, referencia **1920 × 1080**, y Match **0.5**: conservar esos ajustes.

La jerarquía instalada tiene `CurrencyHUD` como **hijo directo de ese Canvas**, al mismo nivel que `BattleHUD`, fuera de `CharacterPanel`, `SkillBar` y `HelmetInventory`:

```text
Canvas  [tag: GameplayHUD]
├── BattleHUD                  (existente)
└── CurrencyHUD                (nuevo)
    ├── XideriaFragmentsCounter
    │   ├── Icon
    │   └── Amount
    └── GoldCounter
        ├── Icon
        └── Amount
```

Colocar `CurrencyHUD` al final de los hijos del Canvas para que sus contadores se dibujen sobre los paneles. El Canvas existente seguirá ocultando el conjunto durante la cortina de transición. No agregar otro Canvas ni otro `PlayerSessionData` al HUD. El menú `Inicio` tiene su propio Canvas y no necesita estos contadores de juego.

## RectTransform y componentes

Valores iniciales de presentación, ajustables según el arte:

1. **CurrencyHUD**: crear un GameObject UI vacío con `RectTransform`.
   - Anchor Min y Max: **(0.5, 1)**, arriba y al centro.
   - Pivot: **(0.5, 1)**.
   - Anchored Position: **(0, -24)**; Scale **(1, 1, 1)** y Rotation **(0, 0, 0)**.
   - Tamaño inicial: **472 × 48**.
   - Agregar `CurrencyHUD`, `Horizontal Layout Group` y `Content Size Fitter`.
   - Horizontal Layout Group: Padding **0**, Spacing **32**, Child Alignment **Middle Center**, Control Child Size Width/Height **activados**, Child Force Expand Width/Height **desactivados**, Reverse Arrangement **desactivado**.
   - Content Size Fitter: Horizontal Fit **Preferred Size**, Vertical Fit **Preferred Size**. Desde ese momento el tamaño lo controla el layout; no desplazar manualmente los contadores.
   - **Spacing** del Horizontal Layout Group controla la separación entre ambos contadores. El conjunto permanece centrado al cambiar ese valor.

2. **XideriaFragmentsCounter** y **GoldCounter**: GameObjects UI vacíos, en ese orden.
   - RectTransform: anchors Min/Max **(0.5, 0.5)**, pivot **(0.5, 0.5)**, escala **1**, rotación **0**. Posición y tamaño finales controlados por el layout del padre.
   - Agregar `Layout Element`: Preferred Width **220**, Preferred Height **48**, Flexible Width/Height **0**.
   - Agregar `Horizontal Layout Group`: Padding **0**, Spacing **8**, Child Alignment **Middle Center**, Control Child Size Width/Height **activados**, Child Force Expand Width/Height **desactivados**.

3. **Icon**, dentro de cada contador: crear mediante **UI > Image**.
   - RectTransform con escala **1**, rotación **0** y pivot **(0.5, 0.5)**; el layout controla posición y tamaño.
   - Agregar `Layout Element`: Preferred Width **32**, Preferred Height **32**, Flexible Width/Height **0**.
   - Image Type **Simple**, Color **blanco**, Preserve Aspect **activado**, Raycast Target **desactivado**.
   - En `XideriaFragmentsCounter/Icon`, Source Image: **Assets/Images/Pedido/FragmentoXideria.png**.
   - En `GoldCounter/Icon`, Source Image: **Assets/Images/Pedido/Moneda.png**.
   - Son los mismos sprites asignados a las recompensas de puertas del Lobby y Level01. Asignarlos al componente `Image`; el script no busca ni carga assets por nombre.

4. **Amount**, dentro de cada contador: crear mediante **UI > Text - TextMeshPro**.
   - RectTransform con escala **1**, rotación **0** y pivot **(0.5, 0.5)**; el layout controla posición y tamaño.
   - Agregar `Layout Element`: Preferred Width **180**, Preferred Height **48**, Flexible Width/Height **0**.
   - TextMeshPro: texto inicial **0**, Font Asset **Assets/Fonts/TeteTipografi SDF.asset**, Font Size **24**, Auto Size **activado** entre **16 y 24**, alineación **Middle Left**, márgenes **0**, Wrapping **No Wrap**, Color **blanco**, Raycast Target **desactivado**.
   - Esta fuente tiene dígitos anchos. Auto Size permite que entren los diez dígitos de `2147483647` sin invadir el otro contador. Si se aumenta el tamaño mínimo, ampliar también el Preferred Width.

## Referencias de CurrencyHUD

En el componente `CurrencyHUD` del objeto superior conectar:

| Campo | Objeto/componente |
|---|---|
| Xideria Fragments Icon | XideriaFragmentsCounter/Icon → Image |
| Xideria Fragments Text | XideriaFragmentsCounter/Amount → TextMeshProUGUI |
| Gold Icon | GoldCounter/Icon → Image |
| Gold Text | GoldCounter/Amount → TextMeshProUGUI |

No necesita referencias a `CharacterStats`, `BattleHUD`, `GridSystem` ni puertas. Mantener activo el objeto para verlo también durante los turnos enemigos y la exploración.

El prefab **Assets/Prefabs/CurrencyHUD.prefab** ya está creado y conectado en las 13 escenas indicadas. Para un mapa futuro, arrastrar ese prefab al Canvas con tag GameplayHUD. Contiene solo la interfaz, sin cámaras, Canvas ni sesiones persistentes. Evitar agregar una segunda instancia en los mapas que ya lo incluyen.

## Validación en Unity

1. Abrir **Window > General > Test Runner**, pestaña **EditMode**, y ejecutar `CurrencyHUDTests`, `StartingCurrencyTests`, `RunStateTests`, `DoorRewardTests` y `RoomRewardDeliveryTests`.
2. Comenzar Play desde Lobby o Tutorial con una sesión nueva: debe mostrar **10 y 10**. Para probar saldo cero, ejecutar explícitamente `PlayerSessionData.Instance.AbandonRun()` desde código de prueba o el depurador: debe mostrar **0 y 0**. No conectarlo a muerte ni a menús como parte de esta etapa.
3. Desde código de prueba o la ventana Immediate del depurador, ejecutar `PlayerSessionData.Instance.TryAddCurrency(RunCurrency.Gold, 7)` y después `PlayerSessionData.Instance.TryAddCurrency(RunCurrency.XideriaFragments, 3)`: solo debe aumentar la moneda correspondiente. Completar también una sala real para verificar la entrega ya existente.
4. Desactivar `CurrencyHUD`, recibir monedas, y reactivarlo: debe mostrar inmediatamente los totales acumulados. Repetir varias veces; un crédito debe contarse una sola vez y no deben quedar callbacks hacia objetos destruidos.
5. Cambiar de sala varias veces, morir y regresar al Lobby, ganar y volver: conservar los totales, con **una sola** instancia persistente de `PlayerSessionData`. No debe repetirse la asignación de los 10 iniciales.
6. Ejecutar `AbandonRun()` con ambas monedas presentes: ambos textos deben pasar a **0**, conservar sus suscripciones y seguir actualizándose si se agregan monedas después.
7. Probar Game View en **1920×1080**, **1280×720**, **1024×768**, **2560×1080** y una ventana estrecha. El conjunto debe mantenerse arriba y centrado, sin recortes ni superposición con otros paneles. Ajustar Spacing, margen superior y anchos desde Inspector si el arte lo requiere.
8. Empezar Play directamente en Level01 no crea una sesión: actualmente solo Lobby y Tutorial la incluyen. Sin sesión el componente muestra **0 y 0** y espera su disponibilidad; no crea una sesión alternativa. Para probar las transiciones reales, comenzar por Lobby o Tutorial.

No hay tiendas, compras, guardado en disco, PlayerPrefs ni cambios a la entrega de recompensas en esta etapa.
