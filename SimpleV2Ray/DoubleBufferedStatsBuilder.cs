using System.Text;

namespace SimpleV2Ray
{
    internal class DoubleBufferedStatsBuilder
    {
        private class StatsBuilder
        {
            readonly StringBuilder stringBuilder = new();
            int numLines = 0;

            public string Text
            {
                get
                {
                    return stringBuilder.ToString();
                }
            }

            public int NumLines
            {
                get
                {
                    return numLines;
                }
            }

            public void AppendLine(string? line)
            {
                stringBuilder.AppendLine(line);
                numLines++;
            }

            public void Clear()
            {
                stringBuilder.Clear();
                numLines = 0;
            }
        }

        private StatsBuilder frontStatsBuilder = new();
        private StatsBuilder backStatsBuilder = new();

        public string Text
        {
            get
            {
                return frontStatsBuilder.Text;
            }
        }

        public int NumLines
        {
            get
            {
                return frontStatsBuilder.NumLines;
            }
        }

        public void AppendLine(string? line)
        {
            backStatsBuilder.AppendLine(line);
        }

        public void Clear()
        {
            backStatsBuilder.Clear();
        }

        public void Swap()
        {
            (backStatsBuilder, frontStatsBuilder) = (frontStatsBuilder, backStatsBuilder);
        }
    }
}
