using System;
using System.Collections.Generic;
using UnityEngine;

namespace WallBurst
{
    /// <summary>
    /// ゲーム用シーンの一覧（設定ファイル）。<c>Assets/WallBurst/Resources/WallBurstSceneCatalog.asset</c> にあります。
    /// <para>シーンを増やすときは、ここに1行足して、Build Settings にシーンを入れるだけです。
    /// キャリブレーション用のシーンは入れません（入れても選ばれません）。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "WallBurstSceneCatalog", menuName = "WallBurst/ゲーム用シーンの一覧")]
    public sealed class GameSceneCatalog : ScriptableObject
    {
        /// <summary>Resources から読むときの名前。</summary>
        public const string ResourceName = "WallBurstSceneCatalog";

        /// <summary>
        /// 一覧の1行。
        /// </summary>
        [Serializable]
        public sealed class Entry
        {
            /// <summary>シーン名（ファイル名から .unity を除いたもの）。</summary>
            [Tooltip("シーン名（ファイル名から .unity を除いたもの）")]
            public string sceneName;

            /// <summary>子ども向けの呼び名。入れると、切り替えのときに「つぎは ○○！」と出します。空なら出しません。</summary>
            [Tooltip("子ども向けの呼び名。入れると、切り替えのときに「つぎは ○○！」と出します。空なら出しません。")]
            public string kidsName;

            /// <summary>ランダムの候補に入れるか。</summary>
            [Tooltip("ランダムの候補に入れるか")]
            public bool enabled = true;
        }

        /// <summary>一覧。</summary>
        public List<Entry> entries = new List<Entry>();

        /// <summary>
        /// 一覧を読みます。ファイルが無いときは、エフェクトだけ・クリスマスツリーの2つを使います。
        /// </summary>
        /// <returns>一覧</returns>
        public static GameSceneCatalog Load()
        {
            GameSceneCatalog catalog = Resources.Load<GameSceneCatalog>(ResourceName);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = CreateInstance<GameSceneCatalog>();
            catalog.entries.Add(new Entry { sceneName = SceneNames.EffectOnly, kidsName = string.Empty, enabled = true });
            catalog.entries.Add(new Entry { sceneName = SceneNames.ChristmasTree, kidsName = string.Empty, enabled = true });
            return catalog;
        }

        /// <summary>
        /// ランダムの候補にできるシーン名（使う・ビルドに入っている・キャリブレーションでない）を返します。
        /// </summary>
        /// <returns>シーン名の一覧</returns>
        public List<string> GetPlayableScenes()
        {
            List<string> names = new List<string>();
            foreach (Entry entry in entries)
            {
                if (entry == null || !entry.enabled || string.IsNullOrEmpty(entry.sceneName) || entry.sceneName == SceneNames.Calibration)
                {
                    continue;
                }

                if (!Application.CanStreamedLevelBeLoaded(entry.sceneName))
                {
                    Debug.LogWarning("[WallBurst] シーン「" + entry.sceneName + "」が Build Settings に入っていないため、選びません。");
                    continue;
                }

                names.Add(entry.sceneName);
            }

            return names;
        }

        /// <summary>
        /// 子ども向けの呼び名を返します（無ければ空）。
        /// </summary>
        /// <param name="sceneName">シーン名</param>
        /// <returns>呼び名</returns>
        public string GetKidsName(string sceneName)
        {
            foreach (Entry entry in entries)
            {
                if (entry != null && entry.sceneName == sceneName)
                {
                    return entry.kidsName ?? string.Empty;
                }
            }

            return string.Empty;
        }
    }
}
