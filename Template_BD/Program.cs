using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Data;
using System.Configuration;
using System.Data.SqlClient;

namespace Template_BD {
    internal class Program {
        static void Main(string[] args) {

            string connectionString = ConfigurationManager.ConnectionStrings["Default"].ConnectionString;

            #region TEMPLATE
            SqlConnection sqlConnection = new SqlConnection(connectionString);
            sqlConnection.Open();
            DataTable dt = sqlConnection.GetSchema("Tables");

            // list of tables/views to SELECT, with "%" as wildcard
            List<string> TABLES = new List<string>()
            {
"CMJ.%",
"S%"
            };

            // <<db>> = database name ('.' -> '_')
            // <<schema>> = schema name ('.' -> '_')
            // <<table>> = table name ('.' -> '_')
            // <<name>> = field name
            // <<type_c>> = data type converted by function (see bellow)
            // <<type>> = data type

            string template = @"
namespace teste.declare
{
    public partial class Constants_<<db>>
    {
--TABLE--
        public partial class <<schema>>_<<table>>
        {
--FIELD--
            <<type_c>> <<name>>;
--\FIELD--
        }
--\TABLE--
    }
}
            ".Trim('\r', '\n', ' ', '\t');


            Match m1 = Regex.Match(template, @"^(.*)--TABLE--\r\n(.*)--\\TABLE--\r\n(.*)$", RegexOptions.Singleline);
            string t_global_pre = m1.Groups[1].Value;
            string t_global_pos = m1.Groups[3].Value;
            string t_TABLE = m1.Groups[2].Value;

            Match m2 = Regex.Match(t_TABLE, @"^(.*)--FIELD--\r\n(.*)--\\FIELD--\r\n(.*)$", RegexOptions.Singleline);
            string t_table_pre = m2.Groups[1].Value;
            string t_table_pos = m2.Groups[3].Value;
            string t_FIELD = m2.Groups[2].Value;

            string db = null;

            string r = t_global_pre;

            #region per_TABLE
            var re = new System.Text.RegularExpressions.Regex(LikeToRegular(String.Join(";", TABLES)).Replace(";", "|"));
            foreach (DataRow t in dt.Rows) {
                if (db == null) db = t["TABLE_CATALOG"].ToString();    // take the 1st catalog
                string schema = t["TABLE_SCHEMA"].ToString();
                string table = t["TABLE_NAME"].ToString();
                if (re.IsMatch(table + "." + schema)) {
                    r += t_table_pre;
                    #region per_FIELD

                    // getschema help: https://www.devart.com/dotconnect/sugarcrm/docs/Metadata-GetSchema.html
                    var dc = sqlConnection.GetSchema("Columns", new[] { t["TABLE_CATALOG"].ToString(), schema, table });
                    foreach (DataRow c in dc.Rows) {
                        r += t_FIELD.Replace("<<name>>", Identifier_Name(c["COLUMN_NAME"].ToString()))
                                    .Replace("<<type>>", c["DATA_TYPE"].ToString())
                                    .Replace("<<null>>", c["IS_NULLABLE"].ToString() == "YES" ? "?" : "")
                                    .Replace("<<type_c>>", ConvertDataType(c["DATA_TYPE"].ToString(),
                                            c.IsNull("CHARACTER_MAXIMUM_LENGTH") ? 0 : (int)c["CHARACTER_MAXIMUM_LENGTH"],
                                            c.IsNull("NUMERIC_PRECISION") ? 0 : (int)(byte)c["NUMERIC_PRECISION"],
                                            c.IsNull("NUMERIC_SCALE") ? 0 : (int)c["NUMERIC_SCALE"],
                                            c["IS_NULLABLE"].ToString() == "YES"));
                    }
                    #endregion

                    r += t_table_pos;
                    r = r.Replace("<<schema>>", Identifier_Name(schema)).
                          Replace("<<table>>", Identifier_Name(table));
                }
            }
            #endregion

            r += t_global_pos;
            r = r.Replace("<<db>>", Identifier_Name(db));

            #endregion

            Console.WriteLine(r);

            Console.ReadKey();
        }

        static string LikeToRegular(string value) {
            return "^" + Regex.Escape(value).Replace("%", ".*") + "$";
        }
        static string Identifier_Name(string value) {
            string reserverdwords = ",abstract,as,base,bool,break,byte,case,catch,char,checked,class,const,continue,decimal,default," +
                "delegate,do,double,else,enum,event,explicit,extern,false,finally,fixed,float,for,foreach,goto,if,implicit,in,int," +
                "interface,internal,is,lock,long,namespace,new,null,object,operator,out,override,params,private,protected,public," +
                "readonly,ref,return,sbyte,sealed,short,sizeof,stackalloc,static,string,struct,switch,this,throw,true,try,typeof,uint," +
                "ulong,unchecked,unsafe,ushort,using,virtual,void,volatile,while,";
            if (string.IsNullOrEmpty(value)) {
                return "--NULL--";
            } else {
                if (reserverdwords.Contains("," + value + ",")) value = "@" + value;
                if (char.IsDigit(value[0])) value = "_" + value;
                value = new Regex(@"[;,\t \.]|[\n]{2}").Replace(value,"_");
                return value;
            }
        }

        static string ConvertDataType(string type, int len, int precision, int scale, bool acceptnull) {
            Dictionary<string, string> dic = new Dictionary<string, string>() {
                { "bigint", "ulong?" },
                { "binary", "byte[%p]" },
                { "bit", "bool?" },
                { "char", "char?" },
                { "date", "System.DateTime" },
                { "datetime", "System.DateTime" },
                { "datetime2", "System.DateTime" },
                { "datetimeoffset", "System.DateTimeOffset" },
                { "decimal", "decimal?" },
                { "filestream", "byte[]" },
                { "float", "double?" },
                { "image", "byte[]" },
                { "int", "int?" },
                { "money", "decimal?" },
                { "nchar", "char?" },
                { "ntext", "decimal?" },
                { "numeric", "decimal?" },
                { "nvarchar", "string" },
                { "real", "float?" },
                { "rowversion", "byte[]" },
                { "smalldatetime", "System.DateTime" },
                { "smallint", "short?" },
                { "smallmoney", "decimal?" },
                { "sql_variant", "object" },
                { "text", "string" },
                { "time", "TimeSpan" },
                { "timestamp", "byte[]" },
                { "tinyint", "byte?" },
                { "uniqueidentifier", "Guid?" },
                { "varbinary", "byte[]" },
                { "varchar", "string" },
                { "xml", "Xml" }
            };
            string t;
            if (!dic.TryGetValue(type, out t)) t = "**" + type;
            if (!acceptnull) t = t.Replace("?", "");
            return t.Replace("%p", precision.ToString()).Replace("%l", len.ToString()).Replace("%s", scale.ToString());
        }



    }
}
