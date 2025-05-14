using SudokuSolver;

namespace SudokuSolver
{
    public enum KandokuSymbol
    {
        臨 = 1, 兵 = 2, 闘 = 3, 者 = 4, 皆 = 5, 陣 = 6, 列 = 7, 在 = 8, 前 = 9
    }

    // DLXアルゴリズムで使うノードのクラス
    public class DLXNode
    {
        public DLXNode Left, Right, Up, Down; // 隣接ノードへの参照
        public ColumnNode Column { get; set; } = null!; // このノードが属する列
        public int RowID; // 行ID
        public DLXNode() => Left = Right = Up = Down = this; // 自己参照による初期化
    }

    // 列ノードのクラス（DLXNode継承）
    public class ColumnNode : DLXNode
    {
        public int Size; // この列に属するノード数
        public string Name; // 列名
        public ColumnNode(string name)
        {
            Column = this;
            Name = name;
            Size = 0;
        }
    }

    // DLXアルゴリズムによるスドク解法・生成クラス
    public class DLXSolver
    {
        private readonly ColumnNode header; // ヘッダノード
        private readonly Random rng = new();

        public DLXSolver() => header = BuildExactCoverMatrix(); // 行列構築

        // ９マスのsudoku制約を表す行列構築
        private static ColumnNode BuildExactCoverMatrix()
        {
            const int totalColumns = 4 * 81;
            var columnList = new ColumnNode[totalColumns];

            // ヘッダノードを作成
            var head = new ColumnNode("head");
            ColumnNode previousColumn = head;

            // 列ノードを作成し、双方向リストで接続
            foreach (int columnIndex in Enumerable.Range(0, totalColumns))
            {
                var columnNode = new ColumnNode(columnIndex.ToString());
                columnList[columnIndex] = columnNode;
                previousColumn.Right = columnNode;
                columnNode.Left = previousColumn;
                previousColumn = columnNode;
            }

            // 最後の列ノードとヘッダノードを接続して環状リストにする
            previousColumn.Right = head;
            head.Left = previousColumn;

            // 各マス(row, col)に対して、1～9の数字(num)を割り当てる候補行を追加
            foreach (var (row, col, num) in
              from row in Enumerable.Range(0, 9)
              from col in Enumerable.Range(0, 9)
              from num in Enumerable.Range(1, 9)
              select (row, col, num))
            {
                // ブロック番号を計算
                int block = row / 3 * 3 + (col / 3);
                // 各制約に対応する列インデックスを計算
                int[] columnIndices = [
                  // 各マスに1つの数字
                  row * 9 + col,
                    // 各行に各数字が1回
                    81 + row * 9 + (num - 1),
                    // 各列に各数字が1回
                    2 * 81 + col * 9 + (num - 1),
                    // 各ブロックに各数字が1回
                    3 * 81 + block * 9 + (num - 1)
                ];
                // 候補行をDLX行列に追加
                AddDLXRow(columnList, row, col, num, columnIndices);
            }

            // ヘッダノードを返す
            return head;
        }

        // 1行（候補）をDLX行列に追加
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

        // 列カバー（DLX操作）
        private static void Cover(ColumnNode col)
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

        // 列カバーの復元
        private static void Uncover(ColumnNode col)
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

        // DLXアルゴリズムによる探索（exact cover 解を深さ優先で探索）
        // solution: 現時点で選択されている候補行のリスト
        private bool Search(List<DLXNode> solution)
        {
            // ベースケース：すべての制約列がカバーされている（＝完全な解）
            if (header.Right == header) return true;

            // 最小サイズ（候補数が最も少ない）列を選択（分岐数を最小化するため）
            ColumnNode c = (ColumnNode)header.Right;
            for (ColumnNode j = (ColumnNode)c.Right; j != header; j = (ColumnNode)j.Right)
                if (j.Size < c.Size) c = j;

            // 選んだ列（制約）を一時的に行列から除外（＝この制約を満たすことを前提に探索）
            Cover(c);

            // この列に属するすべての行（＝この制約を満たす候補）を収集
            var rows = new List<DLXNode>();
            for (DLXNode r = c.Down; r != c; r = r.Down)
                rows.Add(r);

            // ランダムシャッフル（スドク生成時に多様な盤面を得るため）
            Shuffle(rows);

            // 各候補行について順に試行（＝この候補を解に含めると仮定して探索）
            foreach (var r in rows)
            {
                // 現在の候補を部分解に追加
                solution.Add(r);

                // この候補行に含まれる他の制約（列）をすべてカバー（＝他候補との矛盾を除外）
                foreach (var j in ToEnumerable(r.Right, x => x != r, x => x.Right))
                    Cover(j.Column);

                // 再帰的に残りの問題を探索
                if (Search(solution)) return true;

                // 解が見つからなかった場合：この候補を取り消してバックトラック
                solution.RemoveAt(solution.Count - 1);

                // カバーした列をすべて元に戻す（＝次の候補に備える）
                foreach (var j in ToEnumerable(r.Left, x => x != r, x => x.Left))
                    Uncover(j.Column);
            }

            // この列に属するすべての候補を試しても解が見つからなかった場合：
            // 一時的に除外していた列を復元して探索終了
            Uncover(c);
            return false;
        }


        // ノード列挙用ヘルパ
        private static IEnumerable<DLXNode> ToEnumerable(DLXNode start, Func<DLXNode, bool> pred, Func<DLXNode, DLXNode> next)
        {
            for (var x = start; pred(x); x = next(x))
                yield return x;
        }

        // ランダム並び替え
        private void Shuffle<T>(IList<T> list)
        {
            foreach (var i in Enumerable.Range(1, list.Count - 1).Reverse())
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // カンドク（漢独）1問生成
        public string[,] GenerateKandoku()
        {
            var solution = new List<DLXNode>();
            return Search(solution)
                ? solution.Aggregate(new string[9, 9], (result, node) =>
                {
                    int id = node.RowID;
                    int r = id / 81;
                    int c = id / 9 % 9;
                    int n = (id % 9) + 1;
                    result[r, c] = ((KandokuSymbol)n).ToString();
                    return result;
                })
                : throw new Exception("生成失敗");
        }

        // 指定数だけマスを「?」で隠す
        public static string[,] MaskKandoku(string[,] board, int maskCount)
        {
            if (maskCount > 64 && maskCount < 1)
                throw new Exception("マスク数は64以下");
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
    }
}

public static class Program
{
    public static void Main()
    {
        var solver = new DLXSolver();
        var board = solver.GenerateKandoku(); // 問題生成
        var masked = DLXSolver.MaskKandoku(board, 10); // マス隠し

        Console.WriteLine("\n=== 出題 ===");
        DisplayBoard(masked); // 問題表示

        Console.WriteLine("\n=== 解答 ===");
        DisplayBoard(board); // 答え表示
    }

    // 盤面表示
    private static void DisplayBoard(string[,] board)
    {
        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                Console.Write(board[r, c] + " ");
            }
            Console.WriteLine();
        }
    }
}
