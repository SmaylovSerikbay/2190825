using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

// Простая библиотека для работы с JSON
namespace SimpleJSON
{
    public enum JSONNodeType
    {
        Object,
        Array,
        String,
        Number,
        Boolean,
        Null
    }

    public abstract class JSONNode
    {
        public virtual JSONNode this[string key] { get { return null; } set { } }
        public virtual JSONNode this[int index] { get { return null; } set { } }
        public virtual string Value { get { return ""; } set { } }
        public virtual int Count { get { return 0; } }
        public virtual bool IsObject { get { return false; } }
        public virtual bool IsArray { get { return false; } }
        public virtual bool IsString { get { return false; } }
        public virtual bool IsNumber { get { return false; } }
        public virtual bool IsBoolean { get { return false; } }
        public virtual bool IsNull { get { return false; } }
        public virtual JSONNodeType NodeType { get { return JSONNodeType.Null; } }
        
        public virtual void Add(string key, JSONNode item) { }
        public virtual void Add(JSONNode item) { }
        public virtual bool HasKey(string key) { return false; }
        
        public virtual string ToString() { return ""; }
        
        public static implicit operator string(JSONNode d) { return d == null ? null : d.Value; }
        public static implicit operator int(JSONNode d) { return d == null ? 0 : int.Parse(d.Value); }
        public static implicit operator float(JSONNode d) { return d == null ? 0 : float.Parse(d.Value); }
        public static implicit operator double(JSONNode d) { return d == null ? 0 : double.Parse(d.Value); }
        public static implicit operator bool(JSONNode d) { return d != null && d.Value.ToLower() == "true"; }
        
        public static JSONNode Parse(string json)
        {
            return JSONParser.Parse(json);
        }
    }
    
    public class JSONObject : JSONNode
    {
        private Dictionary<string, JSONNode> m_Dict = new Dictionary<string, JSONNode>();
        
        public override JSONNode this[string key]
        {
            get { return m_Dict.ContainsKey(key) ? m_Dict[key] : null; }
            set { m_Dict[key] = value; }
        }
        
        public override int Count { get { return m_Dict.Count; } }
        public override bool IsObject { get { return true; } }
        public override JSONNodeType NodeType { get { return JSONNodeType.Object; } }
        
        public override void Add(string key, JSONNode item)
        {
            m_Dict[key] = item;
        }
        
        public override bool HasKey(string key)
        {
            return m_Dict.ContainsKey(key);
        }
        
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            bool first = true;
            foreach (KeyValuePair<string, JSONNode> kvp in m_Dict)
            {
                if (!first)
                    sb.Append(",");
                first = false;
                sb.Append("\"").Append(kvp.Key).Append("\":");
                sb.Append(kvp.Value.ToString());
            }
            sb.Append("}");
            return sb.ToString();
        }
    }
    
    public class JSONArray : JSONNode
    {
        private List<JSONNode> m_List = new List<JSONNode>();
        
        public override JSONNode this[int index]
        {
            get { return index >= 0 && index < m_List.Count ? m_List[index] : null; }
            set { while (m_List.Count <= index) m_List.Add(null); m_List[index] = value; }
        }
        
        public override int Count { get { return m_List.Count; } }
        public override bool IsArray { get { return true; } }
        public override JSONNodeType NodeType { get { return JSONNodeType.Array; } }
        
        public override void Add(JSONNode item)
        {
            m_List.Add(item);
        }
        
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            bool first = true;
            foreach (JSONNode node in m_List)
            {
                if (!first)
                    sb.Append(",");
                first = false;
                sb.Append(node.ToString());
            }
            sb.Append("]");
            return sb.ToString();
        }
    }
    
    public class JSONString : JSONNode
    {
        private string m_Value;
        
        public JSONString(string value)
        {
            m_Value = value;
        }
        
        public override string Value { get { return m_Value; } set { m_Value = value; } }
        public override bool IsString { get { return true; } }
        public override JSONNodeType NodeType { get { return JSONNodeType.String; } }
        
        public override string ToString()
        {
            return "\"" + m_Value + "\"";
        }
    }
    
    public class JSONNumber : JSONNode
    {
        private string m_Value;
        
        public JSONNumber(string value)
        {
            m_Value = value;
        }
        
        public override string Value { get { return m_Value; } set { m_Value = value; } }
        public override bool IsNumber { get { return true; } }
        public override JSONNodeType NodeType { get { return JSONNodeType.Number; } }
        
        public override string ToString()
        {
            return m_Value;
        }
    }
    
    public class JSONBool : JSONNode
    {
        private bool m_Value;
        
        public JSONBool(bool value)
        {
            m_Value = value;
        }
        
        public override string Value { get { return m_Value ? "true" : "false"; } set { m_Value = value.ToLower() == "true"; } }
        public override bool IsBoolean { get { return true; } }
        public override JSONNodeType NodeType { get { return JSONNodeType.Boolean; } }
        
        public override string ToString()
        {
            return m_Value ? "true" : "false";
        }
    }
    
    public class JSONNull : JSONNode
    {
        public override bool IsNull { get { return true; } }
        public override JSONNodeType NodeType { get { return JSONNodeType.Null; } }
        
        public override string ToString()
        {
            return "null";
        }
    }
    
    internal class JSONParser
    {
        private string m_Json;
        private int m_Pos;
        
        public static JSONNode Parse(string json)
        {
            JSONParser parser = new JSONParser(json);
            return parser.ParseValue();
        }
        
        public JSONParser(string json)
        {
            m_Json = json;
            m_Pos = 0;
        }
        
        private char NextChar
        {
            get
            {
                if (m_Pos < m_Json.Length)
                    return m_Json[m_Pos];
                return '\0';
            }
        }
        
        private void SkipWhitespace()
        {
            while (m_Pos < m_Json.Length && char.IsWhiteSpace(m_Json[m_Pos]))
                m_Pos++;
        }
        
        private JSONNode ParseValue()
        {
            SkipWhitespace();
            
            char c = NextChar;
            
            if (c == '{')
                return ParseObject();
            else if (c == '[')
                return ParseArray();
            else if (c == '\"')
                return ParseString();
            else if (char.IsDigit(c) || c == '-')
                return ParseNumber();
            else if (c == 't' || c == 'f')
                return ParseBoolean();
            else if (c == 'n')
                return ParseNull();
            
            return null;
        }
        
        private JSONObject ParseObject()
        {
            JSONObject obj = new JSONObject();
            m_Pos++; // Skip '{'
            
            SkipWhitespace();
            
            while (m_Pos < m_Json.Length && NextChar != '}')
            {
                string key = ParseString().Value;
                
                SkipWhitespace();
                
                if (NextChar != ':')
                    throw new Exception("Expected ':' in object at position " + m_Pos);
                
                m_Pos++; // Skip ':'
                
                JSONNode value = ParseValue();
                obj.Add(key, value);
                
                SkipWhitespace();
                
                if (NextChar == ',')
                    m_Pos++; // Skip ','
                
                SkipWhitespace();
            }
            
            m_Pos++; // Skip '}'
            
            return obj;
        }
        
        private JSONArray ParseArray()
        {
            JSONArray array = new JSONArray();
            m_Pos++; // Skip '['
            
            SkipWhitespace();
            
            while (m_Pos < m_Json.Length && NextChar != ']')
            {
                JSONNode value = ParseValue();
                array.Add(value);
                
                SkipWhitespace();
                
                if (NextChar == ',')
                    m_Pos++; // Skip ','
                
                SkipWhitespace();
            }
            
            m_Pos++; // Skip ']'
            
            return array;
        }
        
        private JSONString ParseString()
        {
            m_Pos++; // Skip '"'
            
            int start = m_Pos;
            while (m_Pos < m_Json.Length && m_Json[m_Pos] != '\"')
                m_Pos++;
            
            string value = m_Json.Substring(start, m_Pos - start);
            m_Pos++; // Skip '"'
            
            return new JSONString(value);
        }
        
        private JSONNumber ParseNumber()
        {
            int start = m_Pos;
            while (m_Pos < m_Json.Length && (char.IsDigit(m_Json[m_Pos]) || m_Json[m_Pos] == '.' || m_Json[m_Pos] == '-' || m_Json[m_Pos] == 'e' || m_Json[m_Pos] == 'E' || m_Json[m_Pos] == '+'))
                m_Pos++;
            
            string value = m_Json.Substring(start, m_Pos - start);
            return new JSONNumber(value);
        }
        
        private JSONBool ParseBoolean()
        {
            if (m_Json.Substring(m_Pos, 4) == "true")
            {
                m_Pos += 4;
                return new JSONBool(true);
            }
            else if (m_Json.Substring(m_Pos, 5) == "false")
            {
                m_Pos += 5;
                return new JSONBool(false);
            }
            
            throw new Exception("Expected 'true' or 'false' at position " + m_Pos);
        }
        
        private JSONNull ParseNull()
        {
            if (m_Json.Substring(m_Pos, 4) == "null")
            {
                m_Pos += 4;
                return new JSONNull();
            }
            
            throw new Exception("Expected 'null' at position " + m_Pos);
        }
    }
} 