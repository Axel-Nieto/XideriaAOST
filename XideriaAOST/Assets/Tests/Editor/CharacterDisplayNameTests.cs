using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public class CharacterDisplayNameTests : UpgradeIntegrationFixture
{
    [TestCase("Larva_Aire(Clone)", "Larva de Viento")]
    [TestCase("Larva Aire(Clone) (2)", "Larva de Viento")]
    [TestCase("LarbaAire(Clone)", "Larva de Viento")]
    [TestCase("Larva_Tierra(Clone)", "Larva de Tierra")]
    [TestCase("LarbaTierra", "Larva de Tierra")]
    [TestCase("Larva_Fuego(Clone)", "Larva de Fuego")]
    [TestCase("LarbaVeneno(Clone)", "Larva de Veneno")]
    [TestCase("Larva(Clone)", "Larva")]
    [TestCase("BossZone1Combat(Clone)", "Jefe de la Zona 1")]
    [TestCase("1-Boss", "Jefe de la Zona 1")]
    [TestCase("Otro_Enemigo(Clone) (3)", "Otro Enemigo")]
    public void BothEnemyPanelsUseTheReadableNameAndPreserveTheInternalName(string internalName, string expected)
    {
        Character actor = Actor(internalName);
        Assert.That(actor.DisplayName, Is.EqualTo(expected));
        var hover = Child("Enemy panel").AddComponent<EnemyHoverUI>();
        var hoverText = Child("Enemy name", true).AddComponent<TextMeshProUGUI>();
        Set(hover, "enemyNameText", hoverText); hover.Show(actor);
        Assert.That(hoverText.text, Is.EqualTo(expected));
        var info = Child("Info panel").AddComponent<CharacterInfoHUD>();
        var infoText = Child("Character name", true).AddComponent<TextMeshProUGUI>();
        Set(info, "infoText", infoText); info.Show(actor);
        Assert.That(infoText.text.Split('\n')[0], Is.EqualTo(expected));
        Assert.That(actor.name, Is.EqualTo(internalName));
    }

    [TestCase(LarvaElement.Air, "Larva de Viento")]
    [TestCase(LarvaElement.Earth, "Larva de Tierra")]
    [TestCase(LarvaElement.Fire, "Larva de Fuego")]
    public void ElementalNamesFollowTheConfiguredEnemyEvenIfItsObjectWasRenamed(LarvaElement element, string expected)
    {
        Character actor = Actor("Enemy Instance(Clone)");
        Set(actor.gameObject.AddComponent<ElementalLarvaSetup>(), "element", element);
        Assert.That(actor.DisplayName, Is.EqualTo(expected));
    }

    [Test]
    public void PlayerKeepsItsOwnName()
    {
        Character actor = Actor("Sir Tete(Clone)"); Set(actor, "isPlayer", true);
        Assert.That(actor.DisplayName, Is.EqualTo("Sir Tete"));
    }

    private Character Actor(string name)
    {
        Character actor = Child(name).AddComponent<Character>(); actor.enabled = false;
        typeof(Character).GetProperty(nameof(Character.Stats)).SetValue(actor, new CharacterStats(5, 2, 64, 8, 8));
        return actor;
    }
    private GameObject Child(string name, bool ui = false)
    {
        var go = ui ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
        go.SetActive(false); go.transform.SetParent(root.transform, false); return go;
    }
}
