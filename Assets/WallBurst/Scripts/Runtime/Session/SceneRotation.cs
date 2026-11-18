using System;
using System.Collections.Generic;

namespace WallBurst
{
    /// <summary>
    /// 次のゲーム用シーンを選ぶ決まりです（画面に関係しないので、テストで確かめられます）。
    /// <list type="bullet">
    /// <item>候補の中からランダムに1つ選ぶ</item>
    /// <item>すぐ前と同じシーンは選ばない（候補が1つしかないときだけ、同じシーンになる）</item>
    /// <item>今は2つなので交互になります。3つ以上に増やすと、ランダムらしくなります</item>
    /// </list>
    /// </summary>
    public static class SceneRotation
    {
        private static readonly Random SharedRandom = new Random();

        /// <summary>
        /// 次のシーン名を選びます。
        /// </summary>
        /// <param name="candidates">選べるシーン名（キャリブレーションは入れない）</param>
        /// <param name="currentScene">今のシーン名（最初は null）</param>
        /// <param name="random">乱数（省略すると共通のもの）</param>
        /// <returns>次のシーン名（候補が無いときは null）</returns>
        public static string PickNext(IList<string> candidates, string currentScene, Random random = null)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            Random source = random ?? SharedRandom;
            List<string> choices = new List<string>();
            foreach (string scene in candidates)
            {
                if (!string.IsNullOrEmpty(scene) && scene != currentScene && !choices.Contains(scene))
                {
                    choices.Add(scene);
                }
            }

            if (choices.Count == 0)
            {
                return candidates[0];
            }

            return choices[source.Next(choices.Count)];
        }
    }
}
