using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// ゲームで使うアセット（ツリー・飾り・スター）の参照をまとめた設定ファイル。
    /// <c>Assets/WallBurst/Resources/WallBurstAssets.asset</c> にあり、メニューの「WallBurst/セットアップ」で自動で埋まります。
    /// </summary>
    [CreateAssetMenu(fileName = "WallBurstAssets", menuName = "WallBurst/アセットの参照")]
    public sealed class WallBurstAssets : ScriptableObject
    {
        /// <summary>Resources から読むときの名前。</summary>
        public const string ResourceName = "WallBurstAssets";

        /// <summary>ツリー（飾りの無いもの）。Christmas_Tree_Decorator の Spruce2。</summary>
        [Tooltip("ツリー（飾りの無いもの）。Christmas_Tree_Decorator の Spruce2")]
        public GameObject treePrefab;

        /// <summary>飾りの玉（赤・青・金など）。</summary>
        [Tooltip("飾りの玉（赤・青・金など）")]
        public GameObject[] ornamentPrefabs = new GameObject[0];

        /// <summary>てっぺんの星。</summary>
        [Tooltip("てっぺんの星")]
        public GameObject starPrefab;

        /// <summary>
        /// 読みます（無ければ null）。
        /// </summary>
        /// <returns>アセットの参照</returns>
        public static WallBurstAssets Load()
        {
            return Resources.Load<WallBurstAssets>(ResourceName);
        }
    }
}
