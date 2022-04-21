using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace KK_Plugins
{
    public partial class ForceHighPoly
    {
        internal static class Hooks
        {

            /// <summary>
            /// Test all coordiantes parts to check if a low poly doesn't exist.
            /// if low poly doesnt exist for an item set HiPoly to true and exit;
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.Initialize))]
            internal static void CheckHiPoly(ChaControl __instance)
            {
                if (__instance.hiPoly || PolySetting.Value == PolyMode.None) return;

                if (PolySetting.Value == PolyMode.Full)
                {
                    ForcedChaInfos.Add(__instance);
                    return;
                }

                var exType = Traverse.Create(__instance).Property("exType");
                var exTypeExists = exType.PropertyExists();
                var coordinate = __instance.chaFile.coordinate;
                for (var i = 0; i < coordinate.Length; i++)
                {
                    var clothParts = coordinate[i].clothes.parts;
                    for (var j = 0; j < clothParts.Length; j++)
                    {
                        if (clothParts[j].id < 10000000) continue;
                        var category = 105;
                        switch (j)
                        {
                            case 0:
                                category = (__instance.sex != 0 || exTypeExists && exType.GetValue<int>() != 1) ? 105 : 503;
                                break;
                            case 7:
                            case 8:
                                category = ((__instance.sex != 0 || exTypeExists && exType.GetValue<int>() != 1) ? 112 : 504) - j;
                                break;
                            default:
                                break;
                        }
                        category += j;
                        var work = __instance.lstCtrl.GetCategoryInfo((ChaListDefine.CategoryNo)category);
                        if (!work.TryGetValue(clothParts[j].id, out var lib))
                        {
                            continue;
                        }
                        else if (category == 105 || category == 107)
                        {
                            var infoInt = lib.GetInfoInt(ChaListDefine.KeyType.Sex);
                            if (__instance.sex == 0 && infoInt == 3 || __instance.sex == 1 && infoInt == 2)
                            {
                                if (clothParts[j].id != 0)
                                {
                                    work.TryGetValue(0, out lib);
                                }
                                if (lib == null)
                                {
                                    continue;
                                }
                            }
                        }
                        var highAssetName = lib.GetInfo(ChaListDefine.KeyType.MainData);
                        if (string.Empty == highAssetName)
                        {
                            continue;
                        }
                        var manifestName = lib.GetInfo(ChaListDefine.KeyType.MainManifest);
                        var assetBundleName = lib.GetInfo(ChaListDefine.KeyType.MainAB);
#if KK
                        Singleton<Manager.Character>.Instance.AddLoadAssetBundle(assetBundleName, manifestName);
#elif KKS
                        Manager.Character.AddLoadAssetBundle(assetBundleName, manifestName);
#endif
                        if (!CommonLib.LoadAsset<UnityEngine.GameObject>(assetBundleName, highAssetName + "_low", false, manifestName) && CommonLib.LoadAsset<UnityEngine.GameObject>(assetBundleName, highAssetName, false, manifestName))
                        {
                            logger.LogWarning($"{__instance.fileParam.fullname} added Hipolycheck");
                            ForcedChaInfos.Add(__instance);
                            return;
                        }
                    }
                }
            }

            /// <summary>
            /// Might not be neccessary because of the first line of the above method, put probably also cheaper than the previous method
            /// Equivilant to last method results of patching AssetBundleManager and trimming _low
            /// </summary>
            /// <param name="__result"></param>
            [HarmonyPostfix, HarmonyPatch(typeof(ChaInfo), nameof(ChaInfo.hiPoly), MethodType.Getter)]
            private static void HiPolyPostfix(ChaInfo __instance, ref bool __result)
            {
                __result |= AnythingLoading && ForcedChaInfos.Any(x => __instance == x) && ChainfoloadDict.TryGetValue(__instance, out var value) && !value;
                /*if (AnythingLoading && ChainfoloadDict.TryGetValue(__instance, out value)) */
                ChainfoloadDict.TryGetValue(__instance, out value);
                logger.LogDebug(string.Format("{0} result? {1} anythingloading? {2} loadingstatus? {3}", __instance.fileParam.fullname, __result, AnythingLoading, !value));
                //logger.LogDebug(string.Format("get {0} loading? {1} {2}", __instance.fileParam.fullname, !value, !value ? System.Environment.StackTrace : ""));
            }

            static Dictionary<ChaInfo, bool> ChainfoloadDict = new Dictionary<ChaInfo, bool>();
            static bool AnythingLoading = true;

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.LoadNoAsync))]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.LoadAsync))]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ReloadNoAsync))]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ReloadAsync))]
            internal static void LoadAsyncPrefix(ChaControl __instance)
            {
                ChainfoloadDict[__instance] = false;
                AnythingLoading = true;
                logger.LogWarning($"Prefix: still loading {ChainfoloadDict.Values.Where(x => !x).Count()}");
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.LoadNoAsync))]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.LoadAsync))]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ReloadNoAsync))]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ReloadAsync))]
            internal static void LoadAsyncPostfix(ChaControl __instance)
            {
                ChainfoloadDict[__instance] = true;
                AnythingLoading = ChainfoloadDict.Values.Any(x => !x);
                logger.LogWarning($"Postfix: still loading {ChainfoloadDict.Values.Where(x => !x).Count()}");
            }

            [HarmonyPrefix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.OnDestroy))]
            internal static void OnDestroyPrefix(ChaControl __instance)
            {
                ForcedChaInfos.Remove(__instance);
                ChainfoloadDict.Remove(__instance);
                AnythingLoading = ChainfoloadDict.Values.Any(x => !x);
            }

            [HarmonyPrefix, HarmonyPatch(typeof(Manager.Character), nameof(Manager.Character.DeleteChara)), HarmonyPriority(Priority.Last)]
            private static void DeleteCharaPrefix(ChaControl cha, bool entryOnly, ref bool __result)
            {
                logger.LogWarning($"Deleting {cha.fileParam.fullname}");
            }

        }
    }
}