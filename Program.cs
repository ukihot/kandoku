using SudokuSolver;

namespace SudokuSolver
{
    public enum KandokuSymbol
    {
        臨 = 1, 兵 = 2, 闘 = 3, 者 = 4, 皆 = 5, 陣 = 6, 列 = 7, 在 = 8, 前 = 9
    }

    public enum KandokuDifficulty
    {
        VeryEasy = 1,
        Easy = 2,
        Normal = 3,
        Hard = 4,
        VeryHard = 5,
        Extreme = 6,
        Spicy = 7,
        Insane = 8,
        Nightmare = 9,
        Unknown = 10
    }

    // DLXノード
    public class DLXNode
    {
        public DLXNode Left, Right, Up, Down;
        public ColumnNode Column { get; set; } = null!;
        public int RowID;
        public DLXNode() => Left = Right = Up = Down = this;
    }

    // 列ノード
    public class ColumnNode : DLXNode
    {
        public int Size;
        public string Name;
        public ColumnNode(string name)
        {
            Column = this;
            Name = name;
            Size = 0;
        }
    }

    // DLX基盤（Exact Cover Matrix構築・DLX操作のみ）
    public class DLXMatrix
    {
        public ColumnNode Header { get; }
        public DLXMatrix()
        {
            Header = BuildExactCoverMatrix();
        }

        private static ColumnNode BuildExactCoverMatrix()
        {
            const int totalColumns = 4 * 81;
            var columnList = new ColumnNode[totalColumns];
            var head = new ColumnNode("head");
            ColumnNode previousColumn = head;

            foreach (int columnIndex in Enumerable.Range(0, totalColumns))
            {
                var columnNode = new ColumnNode(columnIndex.ToString());
                columnList[columnIndex] = columnNode;
                previousColumn.Right = columnNode;
                columnNode.Left = previousColumn;
                previousColumn = columnNode;
            }

            previousColumn.Right = head;
            head.Left = previousColumn;
            foreach (var (row, col, num) in
              from row in Enumerable.Range(0, 9)
              from col in Enumerable.Range(0, 9)
              from num in Enumerable.Range(1, 9)
              select (row, col, num))
            {
                int block = row / 3 * 3 + (col / 3);
                int[] columnIndices = [
                  row * 9 + col,
                    81 + row * 9 + (num - 1),
                    2 * 81 + col * 9 + (num - 1),
                    3 * 81 + block * 9 + (num - 1)
                ];
                AddDLXRow(columnList, row, col, num, columnIndices);
            }

            return head;
        }

        private static void AddDLXRow(ColumnNode[] columnList, int r, int c, int n, int[] colIdx)
        {
            DLXNode? first = null;
            foreach (var idx in colIdx)
            {
                var colNode = columnList[idx];
                var node = new DLXNode
                {
                    Column = colNode,
                    RowID = r * 81 + c * 9 + (n - 1),
                    Down = colNode,
                    Up = colNode.Up
                };
                colNode.Up.Down = node;
                colNode.Up = node;
                colNode.Size++;
                if (first == null)
                {
                    first = node;
                    node.Left = node.Right = node;
                }
                else
                {
                    node.Right = first;
                    node.Left = first.Left;
                    first.Left.Right = node;
                    first.Left = node;
                }
            }
        }

        public static void Cover(ColumnNode col)
        {
            col.Right.Left = col.Left;
            col.Left.Right = col.Right;
            for (DLXNode row = col.Down; row != col; row = row.Down)
                for (DLXNode j = row.Right; j != row; j = j.Right)
                {
                    j.Down.Up = j.Up;
                    j.Up.Down = j.Down;
                    j.Column.Size--;
                }
        }

        public static void Uncover(ColumnNode col)
        {
            for (DLXNode row = col.Up; row != col; row = row.Up)
                for (DLXNode j = row.Left; j != row; j = j.Left)
                {
                    j.Column.Size++;
                    j.Down.Up = j;
                    j.Up.Down = j;
                }
            col.Right.Left = col;
            col.Left.Right = col;
        }

        public static IEnumerable<DLXNode> ToEnumerable(DLXNode start, Func<DLXNode, bool> pred, Func<DLXNode, DLXNode> next)
        {
            for (var x = start; pred(x); x = next(x))
                yield return x;
        }
    }

    // DLX探索（解探索・シャッフルのみ）
    public class DLXSolver(ColumnNode header)
    {
        private readonly ColumnNode header = header;
        private readonly Random rng = new();

        public bool Search(List<DLXNode> solution)
        {
            if (header.Right == header) return true;
            ColumnNode c = (ColumnNode)header.Right;
            for (ColumnNode j = (ColumnNode)c.Right; j != header; j = (ColumnNode)j.Right)
                if (j.Size < c.Size) c = j;
            DLXMatrix.Cover(c);
            var rows = new List<DLXNode>();
            for (DLXNode r = c.Down; r != c; r = r.Down)
                rows.Add(r);
            Shuffle(rows);
            foreach (var r in rows)
            {
                solution.Add(r);
                foreach (var j in DLXMatrix.ToEnumerable(r.Right, x => x != r, x => x.Right))
                    DLXMatrix.Cover(j.Column);
                if (Search(solution)) return true;
                solution.RemoveAt(solution.Count - 1);
                foreach (var j in DLXMatrix.ToEnumerable(r.Left, x => x != r, x => x.Left))
                    DLXMatrix.Uncover(j.Column);
            }
            DLXMatrix.Uncover(c);
            return false;
        }

        private void Shuffle<T>(IList<T> list)
        {
            if (list == null || list.Count <= 1)
                return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    // Kandoku生成・検証・マスク処理
    public static class KandokuGenerator
    {
        public static string[,] GenerateKandoku()
        {
            var matrix = new DLXMatrix();
            var solver = new DLXSolver(matrix.Header);
            var solution = new List<DLXNode>();
            if (!solver.Search(solution))
                throw new Exception("生成失敗");
            return solution.Aggregate(new string[9, 9], (result, node) =>
            {
                int id = node.RowID;
                int r = id / 81;
                int c = id / 9 % 9;
                int n = (id % 9) + 1;
                result[r, c] = ((KandokuSymbol)n).ToString();
                return result;
            });
        }

        public static bool IsValidBoard(string[,] board)
        {
            for (int i = 0; i < 9; i++)
            {
                var rowSet = new HashSet<string>();
                var colSet = new HashSet<string>();
                var blockSet = new HashSet<string>();
                for (int j = 0; j < 9; j++)
                {
                    var rowVal = board[i, j];
                    if (rowVal != null && rowVal != "? " && !rowSet.Add(rowVal))
                        return false;
                    var colVal = board[j, i];
                    if (colVal != null && colVal != "? " && !colSet.Add(colVal))
                        return false;
                    int br = i / 3 * 3 + (j / 3);
                    int bc = i % 3 * 3 + (j % 3);
                    var blockVal = board[br, bc];
                    if (blockVal != null && blockVal != "? " && !blockSet.Add(blockVal))
                        return false;
                }
            }
            return true;
        }

        public static string[,] MaskKandoku(string[,] board, KandokuDifficulty difficulty)
        {
            int maskCount = GetMaskCount(difficulty);
            var rng = new Random();
            var positions = Enumerable.Range(0, 81).OrderBy(_ => rng.Next()).Take(maskCount);
            var masked = (string[,])board.Clone();
            foreach (var pos in positions)
            {
                int r = pos / 9;
                int c = pos % 9;
                masked[r, c] = "? ";
            }
            return masked;
        }

        private static int GetMaskCount(KandokuDifficulty difficulty) => difficulty switch
        {
            KandokuDifficulty.VeryEasy => 39,
            KandokuDifficulty.Easy => 47,
            KandokuDifficulty.Normal => 51,
            KandokuDifficulty.Hard => 54,
            KandokuDifficulty.VeryHard => 56,
            KandokuDifficulty.Extreme => 58,
            KandokuDifficulty.Spicy => 60,
            KandokuDifficulty.Insane => 62,
            KandokuDifficulty.Nightmare => 63,
            KandokuDifficulty.Unknown => 64,
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
        };
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        int diffNum = 3;
        if (args.Length > 0 && int.TryParse(args[0], out int n) && n >= 1 && n <= 10)
        {
            diffNum = n;
        }
        var difficulty = (KandokuDifficulty)diffNum;

        var board = KandokuGenerator.GenerateKandoku();

        if (!KandokuGenerator.IsValidBoard(board))
        {
            Console.WriteLine("生成された盤面が要件を満たしていません。");
            throw new Exception("盤面検証失敗");
        }

        var masked = KandokuGenerator.MaskKandoku(board, difficulty);

        Console.WriteLine($"\n=== 出題 (難易度: {difficulty}) ===");
        DisplayBoard(masked);

        Console.WriteLine("\n=== 解答 ===");
        DisplayBoard(board);
    }

    private static void DisplayBoard(string[,] board)
    {
        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                Console.Write($"{board[r, c],-1} ");
            }
            Console.WriteLine();
        }
    }
}
