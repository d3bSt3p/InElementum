using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Pictos;
using InElementum.Data.Progression;
using InElementum.Data.Settings;
using InElementum.Editor;
using NUnit.Framework;
using UnityEditor;

namespace InElementum.Tests.EditMode
{
    public class AuthoringTests
    {
        private const string TempFolder = "Assets/_Project/__AuthoringTestTemp";

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(TempFolder);

        [Test]
        public void AssetOpsCreateDuplicateAndDeleteEveryAuthoredType()
        {
            var types = new[]
            {
                typeof(AttackData), typeof(AbilityData), typeof(ItemData), typeof(SurgeData),
                typeof(PictoData), typeof(PassiveData), typeof(EnemyCharacterData)
            };

            foreach (var type in types)
            {
                var asset = DataAssetOps.Create(type, $"{TempFolder}/{type.Name}", "New Asset");
                Assert.That(asset, Is.Not.Null, type.Name);
                Assert.That(AssetDatabase.GetAssetPath(asset), Is.EqualTo($"{TempFolder}/{type.Name}/New Asset.asset"));

                var second = DataAssetOps.Create(type, $"{TempFolder}/{type.Name}", "New Asset");
                Assert.That(AssetDatabase.GetAssetPath(second), Is.Not.EqualTo(AssetDatabase.GetAssetPath(asset)), "Names must not collide.");

                var copy = DataAssetOps.Duplicate(asset);
                Assert.That(copy, Is.Not.Null.And.InstanceOf(type), type.Name);
                Assert.That(copy, Is.Not.SameAs(asset));

                var path = AssetDatabase.GetAssetPath(copy);
                Assert.That(DataAssetOps.Delete(copy), Is.True, type.Name);
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Null, type.Name);
            }
        }

        [Test]
        public void FindAllLocatesTheAssetsEachEditorWindowLists()
        {
            AssertFound<AttackData>();
            AssertFound<AbilityData>();
            AssertFound<ItemData>();
            AssertFound<SurgeData>();
            AssertFound<PictoData>();
            AssertFound<EnemyCharacterData>();
        }

        private static void AssertFound<T>() where T : UnityEngine.ScriptableObject
        {
            var asset = DataAssetOps.Create<T>($"{TempFolder}/{typeof(T).Name}", "Find Me");
            Assert.That(DataAssetOps.FindAll<T>(), Does.Contain(asset), typeof(T).Name);
        }
    }
}
