# Mejoras de Sir Tete: etapa 1

Esta etapa implementa compra, propiedad durante la run y activación de Impulso Vital. No incorpora interfaz, interacción con Shop, cambios de movimiento ni guardado en disco. La condición de abrir la tienda solamente al terminar el movimiento queda para la etapa de interacción.

## Definición y compra

Para preparar una definición manualmente en Unity: Create > Xideria > Upgrade Definition. Configurar:

- Upgrade Id: `sir_tete.vital_impulse` (identidad estable, no el nombre visible).
- Display Name: `Impulso Vital`.
- Description: texto para la futura tienda.
- Icon: puede reutilizarse `Assets/Images/SkillIcons/Energy 1.png`.
- Fragment Cost: costo positivo elegido para pruebas. Cero y valores negativos se rechazan; no se fija balance definitivo.
- Upgrade Type: `VitalImpulse`.

No se creó ni modificó ningún asset de definición automáticamente. La nueva clase permite configurarlo en una etapa posterior.

La API para la futura tienda es:

```csharp
bool purchased = PlayerSessionData.Instance.TryPurchaseUpgrade(definition, out UpgradePurchaseResult result);
bool owned = PlayerSessionData.Instance.HasUpgrade(UpgradeDefinition.VitalImpulseId);
```

No hay que descontar monedas ni registrar propiedad desde el consumidor. La operación central valida definición, ID/tipo implementado, costo positivo, propiedad previa y saldo. `RunState` agrega el ID y descuenta exclusivamente fragmentos en una operación síncrona sin callbacks intermedios. Todos los rechazos dejan ambos saldos y la propiedad intactos. Incluso un costo `int.MaxValue` solo se acepta con saldo suficiente; no hay resta negativa ni desbordamiento.

Después del commit se notifican `BalanceChanged(XideriaFragments, saldo)` y `UpgradesChanged`. Un intento de compra repetido desde esos eventos ya encuentra la mejora comprada. Las excepciones de observadores se registran y se continúa notificando; no transforman una compra confirmada en un resultado fallido. La operación se usa desde el hilo principal de Unity.

El tipo reservado `StatIncrease` no se puede comprar todavía. Los futuros tipos necesitan validación y activación propias antes de habilitar su compra.

## Propiedad y activación

`RunState` serializa `purchasedUpgradeIds`, una lista de strings, sin referencias a escenas o assets. Su vista pública es de solo lectura. El esquema es versión 3; al deserializar un estado anterior, la lista ausente queda vacía. Se eliminan IDs vacíos y duplicados, conservando IDs desconocidos para compatibilidad futura.

La instancia persistente existente de `PlayerSessionData` conserva ese estado entre escenas. `ResetSession()` sigue reiniciando solo datos temporales: no elimina las compras. No se agregaron llamadas a `AbandonRun()` en muerte, victoria, Lobby ni transiciones.

`Character.VitalImpulseEnabled` consulta la propiedad directamente. Ya no existe `enableVitalImpulse` ni se agrega la pasiva al iniciar. `GetPassiveEffects()` devuelve una única instancia efectiva del `VitalImpulsePassive` existente cuando corresponde, filtrando copias agregadas por otras listas. La consulta funciona de inmediato para un personaje ya instanciado, sin depender de `Start`, de cargar datos temporales ni de suscripciones nuevas.

`AbandonRun()` vacía las compras junto con los demás datos de la run y notifica `UpgradesChanged` aunque ya no queden monedas. El personaje existente deja de disponer de la pasiva inmediatamente; incluso una referencia antigua al objeto pasiva comprueba de nuevo la propiedad antes de recuperar energía. La asignación inicial de 10 de cada moneda y el comportamiento de dejar cero al abandonar se conservan.

No se modificaron `VitalImpulsePassive`, `Character.DieRoutine`, la animación de muerte, el evento de recuperación ni los componentes de texto flotante. La recuperación sigue siendo de hasta 2 por enemigo al finalizar su secuencia de muerte, con el importe real emitido a la presentación existente. El HUD existente detecta el cambio de `VitalImpulseEnabled` sin cambios en sus scripts.

## Validación

Ejecutado fuera del editor con el compilador y ensamblados de Unity 6000.3.9f1:

- Compilación de todos los scripts de ejecución y de los tests de Editor: correcta.
- 66 casos de lógica administrada: correctos, cero fallos. Incluyen los 49 casos anteriores de monedas, recompensas/entrega y fondos iniciales, más 17 casos de propiedad y transacción: estado inicial, saldo exacto/insuficiente, límites numéricos, costos/IDs inválidos, duplicados, cambios de identidad de sala, reset y acceso de solo lectura.
- Comparación de hashes contra el estado previo a esta tarea para comprobar que no cambien escenas, prefabs, assets existentes ni sistemas protegidos.

Se agregaron tests de Editor en `UpgradePurchaseTests.cs` y `VitalImpulseUnlockTests.cs`. Los tests nativos están compilados, pero no ejecutados fuera de Unity: usan `ScriptableObject`, una escena de previsualización descartable y `JsonUtility`. No guardan escenas ni assets. Ejecutar desde Window > General > Test Runner > EditMode; seleccionar `RunUpgradeStateTests`, `UpgradePurchaseTests` y `VitalImpulseUnlockTests`. Cubren compra pública y eventos, reentrada, excepciones de observadores, serialización, compatibilidad con snapshots anteriores, activación/desactivación con el mismo personaje, recreación del personaje, recuperación real, enemigos 1x1/2x2 y notificación única después de completar la espera de muerte sin Animator.

Pendiente de comprobar en Play Mode con los assets reales:

1. Una run nueva muestra 10 de oro y 10 fragmentos y Sir Tete no recupera energía por muertes antes de comprar.
2. Invocar la API desde el depurador o un consumidor de prueba con una definición válida: descontar solo su costo en fragmentos y activar Impulso Vital en el personaje existente. Repetir no descuenta otra vez.
3. Matar enemigos 1x1 y 2x2: esperar la animación completa, recuperar hasta 2 una vez por enemigo y mostrar el texto sobre Sir Tete con el color de energía actual. Cerca del máximo debe indicar solo lo recuperado; al máximo no debe aparecer texto.
4. Cambiar de sala, morir, ganar, volver al Lobby y reiniciar datos temporales: conservar saldos restantes y propiedad. No confundir volver al Lobby con abandonar.
5. Invocar `AbandonRun()` con Sir Tete presente: saldos cero, propiedad eliminada y sin nuevas recuperaciones. No hay botón de abandono ni de compra nuevo en esta etapa.

La animación visual real, el posicionamiento/color del texto y las transiciones de escenas no se presentan como pruebas ejecutadas. La tienda y su condición de abrirse solo al terminar el movimiento siguen pendientes de autorización.
