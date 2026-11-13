using UnityEditor;
using UnityEngine;
using WallBurst.Ui;

/// <summary>
/// Textures/Backgrounds の画像を、ビルド用のカタログへ写す。
/// </summary>
public sealed class BackgroundCatalogBuilder : AssetPostprocessor
{
    private const string CatalogPath = "Assets/Resources/BackgroundCatalog.asset";

    /// <summary>
    /// 画像フォルダが変わったときだけカタログを作り直す。
    /// </summary>
    /// <param name="imported">読み込まれたアセット。</param>
    /// <param name="deleted">削除されたアセット。</param>
    /// <param name="moved">移動後のパス。</param>
    /// <param name="movedFrom">移動前のパス。</param>
    private static void OnPostprocessAllAssets(
        string[] imported,
        string[] deleted,
        string[] moved,
        string[] movedFrom)
    {
        if (!TouchesBackgrounds(imported) && !TouchesBackgrounds(deleted) && !TouchesBackgrounds(moved) && !TouchesBackgrounds(movedFrom))
        {
            return;
        }

        Rebuild();
    }

    /// <summary>
    /// フォルダ内の画像をカタログへ入れる。
    /// </summary>
    [MenuItem("Tools/背景カタログを更新")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Texture2D[] images = BackgroundImageLibrary.Load();
        BackgroundCatalog catalog = AssetDatabase.LoadAssetAtPath<BackgroundCatalog>(CatalogPath);
        if (catalog == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            catalog = ScriptableObject.CreateInstance<BackgroundCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        if (SameImages(catalog.Images, images))
        {
            return;
        }

        catalog.Images = images;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 背景フォルダのパスが含まれるか。
    /// </summary>
    /// <param name="paths">アセットパス。</param>
    /// <returns>含まれるなら true。</returns>
    private static bool TouchesBackgrounds(string[] paths)
    {
        if (paths == null)
        {
            return false;
        }

        for (int i = 0; i < paths.Length; i++)
        {
            if (paths[i] != null && paths[i].Replace('\\', '/').Contains("Textures/Backgrounds"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 並びと実体が同じなら書き直さない。
    /// </summary>
    /// <param name="current">今のカタログ。</param>
    /// <param name="next">新しい一覧。</param>
    /// <returns>同じなら true。</returns>
    private static bool SameImages(Texture2D[] current, Texture2D[] next)
    {
        if (current == null || next == null || current.Length != next.Length)
        {
            return false;
        }

        for (int i = 0; i < current.Length; i++)
        {
            if (current[i] != next[i])
            {
                return false;
            }
        }

        return true;
    }
}
