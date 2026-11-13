using System;

namespace WallBurst.Effects
{
    /// <summary>
    /// ヒットのときに出す演出の種類。複数オンなら、その中から1つ選ぶ。
    /// </summary>
    public sealed class EffectSelection
    {
        /// <summary>演出の数。輪、閃光、粒、二重。</summary>
        public const int Count = 4;

        /// <summary>画面に出す名前。番号は WallBurstEffect の種類と同じ。</summary>
        public static readonly string[] Names = { "輪", "閃光", "粒", "二重" };

        private readonly bool[] _enabled = { true, true, true, true };

        /// <summary>
        /// その種類を出すか。
        /// </summary>
        /// <param name="kind">0 から 3。</param>
        /// <returns>出すなら true。</returns>
        public bool IsEnabled(int kind)
        {
            if (kind < 0 || kind >= Count)
            {
                return false;
            }

            return _enabled[kind];
        }

        /// <summary>
        /// オンになっている数。
        /// </summary>
        /// <returns>1 以上。</returns>
        public int EnabledCount()
        {
            int count = 0;
            for (int i = 0; i < Count; i++)
            {
                if (_enabled[i])
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 種類のオンオフを変える。最後の1つはオフにしない。
        /// </summary>
        /// <param name="kind">0 から 3。</param>
        /// <param name="enabled">出すなら true。</param>
        /// <returns>反映したら true。最後の1つを消そうとしたら false。</returns>
        public bool SetEnabled(int kind, bool enabled)
        {
            if (kind < 0 || kind >= Count)
            {
                return false;
            }

            if (!enabled && _enabled[kind] && EnabledCount() <= 1)
            {
                return false;
            }

            _enabled[kind] = enabled;
            return true;
        }

        /// <summary>
        /// 4種類すべてをオンに戻す。
        /// </summary>
        public void EnableAll()
        {
            for (int i = 0; i < Count; i++)
            {
                _enabled[i] = true;
            }
        }

        /// <summary>
        /// 保存した並びを戻す。全部オフの並びは、全部オンにする。
        /// </summary>
        /// <param name="flags">長さ 4。短い、または null なら全部オン。</param>
        public void Apply(bool[] flags)
        {
            if (flags == null || flags.Length < Count)
            {
                EnableAll();
                return;
            }

            bool any = false;
            for (int i = 0; i < Count; i++)
            {
                _enabled[i] = flags[i];
                if (flags[i])
                {
                    any = true;
                }
            }

            if (!any)
            {
                EnableAll();
            }
        }

        /// <summary>
        /// 保存用に、オンオフをコピーする。
        /// </summary>
        /// <returns>長さ 4。</returns>
        public bool[] CopyFlags()
        {
            bool[] flags = new bool[Count];
            for (int i = 0; i < Count; i++)
            {
                flags[i] = _enabled[i];
            }

            return flags;
        }

        /// <summary>
        /// オンのものから1つ選ぶ。
        /// </summary>
        /// <param name="roll">0 以上、オンの数未満を返す。</param>
        /// <returns>演出の種類。</returns>
        public int Pick(Func<int, int> roll)
        {
            int count = EnabledCount();
            if (count <= 0)
            {
                return 0;
            }

            int choice = 0;
            if (roll != null)
            {
                choice = roll(count);
            }

            if (choice < 0 || choice >= count)
            {
                choice = 0;
            }

            int seen = 0;
            for (int i = 0; i < Count; i++)
            {
                if (!_enabled[i])
                {
                    continue;
                }

                if (seen == choice)
                {
                    return i;
                }

                seen++;
            }

            return 0;
        }
    }
}
