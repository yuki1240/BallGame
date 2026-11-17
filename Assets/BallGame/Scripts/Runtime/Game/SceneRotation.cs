using System;
using System.Collections.Generic;

namespace BallGame
{
    /// <summary>
    /// 遊ぶ場面（クリスマス・草原・紅葉の山・シャボン玉割り）を、ランダムな順に替えるための決まりです。
    /// 次の場面は、今の場面以外から、ランダムに1つ選びます（同じ場面が2回つづくことはありません）。
    /// 画面に依存しないので、テストで確かめられます。
    /// </summary>
    public static class SceneRotation
    {
        /// <summary>クリスマスのシーン名。</summary>
        public const string ChristmasScene = "Christmas";

        /// <summary>草原のシーン名。</summary>
        public const string MeadowScene = "Game";

        /// <summary>紅葉の山のシーン名。</summary>
        public const string AutumnScene = "Autumn";

        /// <summary>シャボン玉割りのシーン名。</summary>
        public const string BubbleScene = "Bubble";

        private static readonly string[] Scenes = { ChristmasScene, MeadowScene, AutumnScene, BubbleScene };

        private static readonly Random SharedRandom = new Random();

        /// <summary>
        /// 次の場面のシーン名を、今の場面以外からランダムに選びます。
        /// </summary>
        /// <param name="currentScene">今のシーン名</param>
        /// <param name="random">乱数（省略すると共通のものを使う）</param>
        /// <returns>次のシーン名</returns>
        public static string PickNext(string currentScene, Random random = null)
        {
            Random source = random ?? SharedRandom;
            List<string> candidates = new List<string>();
            foreach (string scene in Scenes)
            {
                if (scene != currentScene)
                {
                    candidates.Add(scene);
                }
            }

            return candidates[source.Next(candidates.Count)];
        }

        /// <summary>
        /// 子ども向けの「つぎは ○○！」の文を返します。
        /// </summary>
        /// <param name="sceneName">次のシーン名</param>
        /// <returns>文</returns>
        public static string GetKidsTitle(string sceneName)
        {
            return "つぎは " + GetKidsName(sceneName) + "！";
        }

        /// <summary>
        /// 操作する人向けの場面の呼び名を返します。
        /// </summary>
        /// <param name="sceneName">シーン名</param>
        /// <returns>呼び名</returns>
        public static string GetOperatorName(string sceneName)
        {
            return sceneName == ChristmasScene ? "クリスマス" : sceneName == MeadowScene ? "草原" : sceneName == AutumnScene ? "紅葉の山" : sceneName == BubbleScene ? "シャボン玉割り" : sceneName;
        }

        private static string GetKidsName(string sceneName)
        {
            return sceneName == ChristmasScene ? "クリスマス" : sceneName == MeadowScene ? "おはなばたけ" : sceneName == AutumnScene ? "もみじの やま" : sceneName == BubbleScene ? "シャボンだま" : sceneName;
        }
    }
}
