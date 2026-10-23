using System.Globalization;

namespace ASPNET.Source.Expression {
    public enum Token {
        LogicOr,
        LogicAnd,
        Equal,
        Not,
        NotEqual,
        Less,
        LessEqual,
        Greater,
        GreaterEqual,
        Add,
        Sub,
        Mul,
        Div,
        Mod,
        LeftParen,
        RightParen,
        True,
        False,
        String,
        Number,
        Identifier,
        EOF
    }
    public struct TokenValue {
        public Token Type;
        public int begin;
        public int end;
        public TokenValue(Token type, int begin, int end) {
            this.Type = type; this.begin = begin; this.end = end;
        }
    }
    public class Tokenizer {
        public string data;
        public int itr;
        public int max;
        public List<TokenValue> values = new();
        public Tokenizer(string expr) {
            this.data = expr;
            this.itr = 0;
            this.max = data.Length;
        }
        public bool Match(char c) {
            if (itr < max && data[itr] == c) {
                ++itr;
                return true;
            }
            return false;
        }
        public void Push(Token token, int begin = 0, int end = 0) {
            values.Add(new(token, begin, end));
        }
        private void ParseNumber(int begin) {
            while (itr < max && char.IsDigit(data[itr])) itr++;
            if (itr < max && data[itr] == '.') {
                itr++;
                if (itr >= max || !char.IsDigit(data[itr]))
                    ErrorHelper.Throw($"小数点后需要数字，位置：{begin}");
                while (itr < max && char.IsDigit(data[itr])) itr++;
            }
            Push(Token.Number, begin, itr);
        }

        private void ParseIdentifier(int begin) {
            while (itr < max && (char.IsLetterOrDigit(data[itr]) || data[itr] == '_')) itr++;
            string text = data.Substring(begin, itr - begin);
            Token token = text switch {
                "true" => Token.True,
                "false" => Token.False,
                _ => Token.Identifier
            };
            Push(token, begin, itr);
        }
        private void ParseString(int begin) {
            itr++;
            while (itr < max && data[itr] != '"') {
                if (data[itr] == '\\' && itr + 1 < max) itr += 2;
                else itr++;
            }
            if (itr >= max) ErrorHelper.Throw($"字符串没有结束引号，位置：{begin}");
            itr++;
            Push(Token.String, begin, itr);
        }

        public List<TokenValue> Split() {
            while (itr < max) {
                char c = data[itr];
                if (char.IsWhiteSpace(c)) {
                    itr++;
                    continue;
                }
                int begin = itr;
                switch (c) {
                    case '|':
                        itr++;
                        if (!Match('|')) ErrorHelper.Throw($"期望第二个 |，位置：{begin}");
                        Push(Token.LogicOr, begin, itr);
                        break;
                    case '&':
                        itr++;
                        if (!Match('&')) ErrorHelper.Throw($"期望第二个 &，位置：{begin}");
                        Push(Token.LogicAnd, begin, itr);
                        break;
                    case '=':
                        itr++;
                        if (!Match('=')) ErrorHelper.Throw($"单独的 = 不是合法运算符，位置：{begin}");
                        Push(Token.Equal, begin, itr);
                        break;
                    case '!':
                        itr++;
                        if (Match('=')) Push(Token.NotEqual, begin, itr);
                        else Push(Token.Not, begin, itr);
                        break;
                    case '<':
                        itr++;
                        if (Match('=')) Push(Token.LessEqual, begin, itr);
                        else Push(Token.Less, begin, itr);
                        break;
                    case '>':
                        itr++;
                        if (Match('=')) Push(Token.GreaterEqual, begin, itr);
                        else Push(Token.Greater, begin, itr);
                        break;
                    case '+':
                        itr++;
                        Push(Token.Add, begin, itr);
                        break;
                    case '-':
                        itr++;
                        Push(Token.Sub, begin, itr);
                        break;
                    case '*':
                        itr++;
                        Push(Token.Mul, begin, itr);
                        break;
                    case '/':
                        itr++;
                        Push(Token.Div, begin, itr);
                        break;
                    case '%':
                        itr++;
                        Push(Token.Mod, begin, itr);
                        break;
                    case '(':
                        itr++;
                        Push(Token.LeftParen, begin, itr);
                        break;
                    case ')':
                        itr++;
                        Push(Token.RightParen, begin, itr);
                        break;
                    case '"':
                        ParseString(begin);
                        break;
                    default:
                        if (char.IsDigit(c)) {
                            ParseNumber(begin);
                        } else if (char.IsLetter(c) || c == '_') {
                            ParseIdentifier(begin);
                        } else {
                            ErrorHelper.Throw($"无法识别的字符：{c}，位置：{begin}");
                        }
                        break;
                }
            }
            Push(Token.EOF, itr, itr);
            return values;
        }
    }
    public record class Node();
    public record class NodeOr(Node Left, Node Right) : Node;
    public record class NodeAnd(Node Left, Node Right) : Node;
    public record class NodeEquality(Node Left, Node Right, bool Condition) : Node;
    public record class NodeComparison(Node Left, Node Right, Token CmpType) : Node;
    public record class NodeAdditive(Node Left, Node Right, Token OpType) : Node;
    public record class NodeMultiply(Node Left, Node Right, Token OpType) : Node;
    public record class NodeUnary(Node Value, Token OpType) : Node;
    public record class NodeNumber(decimal Value) : Node;
    public record class NodeString(string Value) : Node;
    public record class NodeIdentifier(string Ref) : Node;
    public class Parser {
        private string data;
        private List<TokenValue> tokens;
        private int itr;
        public Parser(string str) {
            this.data = str;
            this.tokens = new Tokenizer(str).Split();
        }
        private bool Match(Token token) {
            return itr < tokens.Count && tokens[itr].Type == token;
        }
        private string Current() {
            if (itr >= tokens.Count) return "";
            var token = tokens[itr];
            return data.Substring(token.begin, token.end - token.begin);
        }

        private void ThrowError(string msg = "") {
            int pos = itr < tokens.Count ? tokens[itr].begin : data.Length;
            string text = Current();
            if (text.Length == 0) text = "<EOF>";
            ErrorHelper.Throw($"语法错误:<{pos}> : <{text}> {msg}");
        }

        private Token CurrentType() {
            return tokens[itr].Type;
        }
        private void Consume() {
            ++itr;
        }
        /*
expression     := or
or             := and ( "||" and )*
and            := equality ( "&&" equality )*
equality       := comparison ( ( "==" | "!=" ) comparison )*
comparison     := additive ( ( "<" | "<=" | ">" | ">=" ) additive )*
additive       := multiply ( ( "+" | "-" ) multiply )*
multiply       := unary ( ( "*" | "/" | "%" ) unary )*
unary          := ( "!" | "-" ) unary | primary
primary        := number | string | true | false | identifier | "(" expression ")"
*/
        public Node Expression() {
            itr = 0;
            var node = Or();
            if (!Match(Token.EOF)) ThrowError("表达式结尾有多余内容");
            return node;
        }
        private Node Or() {
            var left = And();
            while (Match(Token.LogicOr)) {
                Consume();
                var right = And();
                left = new NodeOr(left, right);
            }
            return left;
        }
        private Node And() {
            var left = Equality();
            while (Match(Token.LogicAnd)) {
                Consume();
                var right = Equality();
                left = new NodeAnd(left, right);
            }
            return left;
        }
        private Node Equality() {
            var left = Comparison();
            while (Match(Token.Equal) || Match(Token.NotEqual)) {
                bool condition = Match(Token.Equal);
                Consume();
                var right = Comparison();
                left = new NodeEquality(left, right, condition);
            }
            return left;
        }

        private Node Comparison() {
            var left = Additive();
            while (Match(Token.Less) || Match(Token.LessEqual) || Match(Token.Greater) || Match(Token.GreaterEqual)) {
                Token op = CurrentType();
                Consume();
                var right = Additive();
                left = new NodeComparison(left, right, op);
            }
            return left;
        }
        private Node Additive() {
            var left = Multiply();
            while (Match(Token.Add) || Match(Token.Sub)) {
                Token op = CurrentType();
                Consume();
                var right = Multiply();
                left = new NodeAdditive(left, right, op);
            }
            return left;
        }
        private Node Multiply() {
            var left = Unary();
            while (Match(Token.Mul) || Match(Token.Div) || Match(Token.Mod)) {
                Token op = CurrentType();
                Consume();
                var right = Unary();
                left = new NodeMultiply(left, right, op);
            }
            return left;
        }
        private Node Unary() {
            if (Match(Token.Not)) {
                Consume();
                var e = Unary();
                return new NodeUnary(e, Token.Not);
            }
            if (Match(Token.Sub)) {
                Consume();
                var e = Unary();
                return new NodeUnary(e, Token.Sub);
            }
            return Primary();
        }
        private Node Primary() {
            if (Match(Token.Number)) {
                string val = Current();
                Consume();
                return new NodeNumber(decimal.Parse(val, CultureInfo.InvariantCulture));
            }
            if (Match(Token.String)) {
                string val = Current();
                Consume();
                return new NodeString(val.Substring(1, val.Length - 2));
            }
            if (Match(Token.True)) {
                Consume();
                return new NodeNumber(1);
            }
            if (Match(Token.False)) {
                Consume();
                return new NodeNumber(0);
            }
            if (Match(Token.Identifier)) {
                string val = Current();
                Consume();
                return new NodeIdentifier(val);
            }
            if (Match(Token.LeftParen)) {
                Consume();
                var e = Or();
                if (Match(Token.RightParen)) {
                    Consume();
                    return e;
                }
                ThrowError("需要右括号");
            }
            ThrowError("非法的表达式");
            return new Node();
        }
    }
    public record struct FieldValueStruct(string Name, string? SValue, decimal? DValue, long FieldId);
    public class Environment {//目前暂时用AST解析,因为安全,如果遇到性能瓶颈换为表达式树编译或者别的什么
        private Dictionary<string, string> svalue = new();
        private Dictionary<string, decimal> dvalue = new();
        private Dictionary<string, long> fieldId = new();
        private Node expr = null!;
        public void SetValue(string key, decimal value, long fieldId) {
            if (fieldId != 0) {
                dvalue[key] = value;
                this.fieldId[key] = fieldId;
            } else {
                if (this.fieldId.ContainsKey(key)) dvalue[key] = value;
            }
        }

        public void SetValue(string key, string value, long fieldId) {
            if (fieldId != 0) {
                svalue[key] = value;
                this.fieldId[key] = fieldId;
            } else {
                if (this.fieldId.ContainsKey(key)) svalue[key] = value;
            }
        }
        public List<FieldValueStruct> GetData() {
            var list = new List<FieldValueStruct>();
            foreach (var i in svalue) list.Add(new(i.Key, i.Value, null, fieldId[i.Key]));
            foreach (var i in dvalue) list.Add(new(i.Key, null, i.Value, fieldId[i.Key]));
            return list;
        }
        public void Clear() {
            svalue.Clear();
            dvalue.Clear();
            fieldId.Clear();
        }
        public void InitExpr(string expr) {
            this.expr = new Parser(expr).Expression();
        }
        public bool Eval() {
            if (expr == null) throw new Exception("表达式未初始化");
            return IsTruthy(EvalNode(expr));
        }
        private object EvalNode(Node node) {
            return node switch {
                NodeNumber n => n.Value,
                NodeString s => s.Value,
                NodeIdentifier id => GetIdentifierValue(id.Ref),
                NodeUnary u => EvalUnary(u),
                NodeMultiply m => EvalMultiply(m),
                NodeAdditive a => EvalAdditive(a),
                NodeComparison c => EvalComparison(c),
                NodeEquality e => EvalEquality(e),
                NodeAnd and => EvalAnd(and),
                NodeOr or => EvalOr(or),
                _ => throw new Exception($"未知的 AST 节点类型: {node.GetType().Name}")
            };
        }
        private object GetIdentifierValue(string name) {
            if (dvalue.TryGetValue(name, out decimal d)) return d;
            if (svalue.TryGetValue(name, out string? s)) return s ?? string.Empty;
            throw new Exception($"未找到变量: {name}");
        }
        private object EvalUnary(NodeUnary u) {
            var val = EvalNode(u.Value);
            if (u.OpType == Token.Sub) {
                if (val is decimal d) return -d;
                throw new Exception("负号操作符 '-' 只能作用于数字类型");
            }
            if (u.OpType == Token.Not) return IsTruthy(val) ? 0m : 1m;
            throw new Exception($"不支持的一元操作符: {u.OpType}");
        }
        private object EvalMultiply(NodeMultiply m) {
            var left = EvalNode(m.Left);
            var right = EvalNode(m.Right);
            if (left is decimal l && right is decimal r) {
                return m.OpType switch {
                    Token.Mul => l * r,
                    Token.Div => r == 0 ? throw new DivideByZeroException("除数不能为零") : l / r,
                    Token.Mod => r == 0 ? throw new DivideByZeroException("取模除数不能为零") : l % r,
                    _ => throw new Exception($"不支持的乘除操作符: {m.OpType}")
                };
            }
            throw new Exception("乘法、除法和取模操作只能用于数字");
        }
        private object EvalAdditive(NodeAdditive a) {
            var left = EvalNode(a.Left);
            var right = EvalNode(a.Right);
            if (a.OpType == Token.Add) {
                if (left is decimal l && right is decimal r) return l + r;
                return left.ToString()! + right.ToString()!;
            }
            if (a.OpType == Token.Sub) {
                if (left is decimal l && right is decimal r) return l - r;
                throw new Exception("减法操作只能用于数字");
            }
            throw new Exception($"不支持的加减操作符: {a.OpType}");
        }

        private object EvalComparison(NodeComparison c) {
            var left = EvalNode(c.Left);
            var right = EvalNode(c.Right);
            if (left is decimal l && right is decimal r) {
                bool res = c.CmpType switch {
                    Token.Less => l < r,
                    Token.LessEqual => l <= r,
                    Token.Greater => l > r,
                    Token.GreaterEqual => l >= r,
                    _ => throw new Exception($"不支持的比较操作符: {c.CmpType}")
                };
                return res ? 1m : 0m;
            }
            if (left is string ls && right is string rs) {
                int cmp = string.Compare(ls, rs, StringComparison.Ordinal);
                bool res = c.CmpType switch {
                    Token.Less => cmp < 0,
                    Token.LessEqual => cmp <= 0,
                    Token.Greater => cmp > 0,
                    Token.GreaterEqual => cmp >= 0,
                    _ => throw new Exception($"不支持的比较操作符: {c.CmpType}")
                };
                return res ? 1m : 0m;
            }
            throw new Exception("不同数据类型之间无法进行大小比较");
        }

        private object EvalEquality(NodeEquality e) {
            var left = EvalNode(e.Left);
            var right = EvalNode(e.Right);
            bool temp = object.Equals(left, right);
            bool res = e.Condition ? temp : !temp;
            return res ? 1m : 0m;
        }
        private object EvalAnd(NodeAnd and) {
            var left = EvalNode(and.Left);
            if (!IsTruthy(left)) return 0m;
            var right = EvalNode(and.Right);
            return IsTruthy(right) ? 1m : 0m;
        }
        private object EvalOr(NodeOr or) {
            var left = EvalNode(or.Left);
            if (IsTruthy(left)) return 1m;
            var right = EvalNode(or.Right);
            return IsTruthy(right) ? 1m : 0m;
        }
        private bool IsTruthy(object value) {
            if (value is decimal d) return d != 0m;
            if (value is string s) return !string.IsNullOrEmpty(s);
            if (value is bool b) return b;
            return value != null;
        }
    }
}