using CityGame.Core;

namespace CityGame.Runtime
{
    /// <summary>
    /// Every player-facing string in one place (Korean). Swap this class for a string
    /// table when localisation is added.
    /// </summary>
    public static class CityStrings
    {
        public const string GameTitle = "미니 시티";
        public const string Money = "자금";
        public const string Population = "인구";
        public const string Jobs = "일자리";
        public const string Happiness = "행복도";
        public const string Tax = "세율";
        public const string Date = "{0}년 {1}월 {2}일";
        public const string Budget = "지난달 수입 +{0:N0} / 지출 -{1:N0}";
        public const string Demand = "수요";
        public const string Help = "도움말 (H)";
        public const string Saved = "도시를 저장했습니다.";
        public const string Loaded = "도시를 불러왔습니다.";
        public const string NoSave = "저장된 도시가 없습니다.";
        public const string NoFunds = "자금이 부족합니다!";
        public const string NoRoad = "도로 연결 없음 — 성장 불가";
        public const string Milestone = "도시 등급 상승: {0}!";
        public const string MonthReport = "월말 결산: 수입 +{0:N0}, 지출 -{1:N0}";

        public static readonly string[] HelpLines =
        {
            "좌클릭/드래그: 건설 (도로는 칠하기, 구역은 사각형)",
            "1 도로  2 주거  3 상업  4 공업  5 공원  6/X 철거  Esc 선택 해제",
            "WASD/방향키: 이동   Q/E 또는 우클릭 드래그: 회전",
            "마우스 휠 또는 +/-: 확대·축소   휠 클릭 드래그: 이동",
            "Space: 일시정지   [ ]: 속도   F5 저장   F9 불러오기",
            "구역은 도로와 맞닿아야 성장합니다. 공원은 행복도를 높이고",
            "공장은 주변 행복도를 낮춥니다. 행복도가 높아야 고층으로 성장!",
        };

        public static readonly string[] MilestoneNames = { "마을", "소도시", "도시", "대도시", "광역시", "메트로폴리스" };

        /// <summary>Display name of a tool.</summary>
        public static string ToolName(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Road: return "도로";
                case ToolType.Residential: return "주거";
                case ToolType.Commercial: return "상업";
                case ToolType.Industrial: return "공업";
                case ToolType.Park: return "공원";
                case ToolType.Bulldoze: return "철거";
                default: return "선택";
            }
        }

        /// <summary>Display name of a tile type.</summary>
        public static string TileName(TileType type)
        {
            switch (type)
            {
                case TileType.Road: return "도로";
                case TileType.Residential: return "주거 구역";
                case TileType.Commercial: return "상업 구역";
                case TileType.Industrial: return "공업 구역";
                case TileType.Park: return "공원";
                default: return "빈 땅";
            }
        }

        /// <summary>Title for a milestone index.</summary>
        public static string MilestoneName(int index)
        {
            if (index < 0) index = 0;
            return MilestoneNames[index < MilestoneNames.Length ? index : MilestoneNames.Length - 1];
        }
    }
}
