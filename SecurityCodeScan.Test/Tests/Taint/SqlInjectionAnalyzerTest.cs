﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SecurityCodeScan.Analyzers;
using SecurityCodeScan.Analyzers.Taint;
using SecurityCodeScan.Test.Config;
using SecurityCodeScan.Test.Helpers;

namespace SecurityCodeScan.Test.Taint
{
    [TestClass]
    public class SqlInjectionAnalyzerTest : DiagnosticVerifier
    {
        protected override IEnumerable<DiagnosticAnalyzer> GetDiagnosticAnalyzers(string language)
        {
            if (language == LanguageNames.CSharp)
                return new DiagnosticAnalyzer[] { new CSharpAnalyzers(new TaintAnalyzerCSharp()) };
            else
                return new DiagnosticAnalyzer[] { new VBasicAnalyzers(new TaintAnalyzerVisualBasic()) };
        }

        private static readonly PortableExecutableReference[] References =
        {            
            MetadataReference.CreateFromFile(typeof(System.Web.UI.WebControls.SqlDataSource).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Data.Entity.DbContext).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.Practices.EnterpriseLibrary.Data.Sql.SqlDatabase).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.EntityFrameworkCore.RelationalQueryableExtensions).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Data.SQLite.SQLiteCommand).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.Data.Sqlite.SqliteCommand).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Web.Mvc.Controller).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(NHibernate.ISession).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Cassandra.ISession).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Npgsql.NpgsqlCommand).Assembly.Location)
        };

        protected override IEnumerable<MetadataReference> GetAdditionalReferences() => References;

        [TestMethod]
        [Ignore("Full taint analysis is needed")]
        public async Task SqlInjectionEnterpriseLibraryDataParametrized()
        {
            var cSharpTest = @"
using System.Data;
using System.Data.Common;
using Microsoft.Practices.EnterpriseLibrary.Data.Sql;
using System.Web.Mvc;

namespace sample
{
    class MyFoo : Controller
    {
        public MyFoo()
        {
            m_db = new SqlDatabase("""");
        }

        private SqlDatabase m_db;

        private SqlDatabase GetDataBase() { return m_db; }

        public void Run(string input)
        {
            var db = GetDataBase();
            DbCommand cmd = db.GetSqlStringCommand(""SELECT * FROM Users WHERE username = @username and role='user'"");
            db.AddInParameter(cmd, ""@username"", DbType.String, input);
            db.ExecuteDataSet(cmd);
        }
    }
}
";

            var visualBasicTest = @"
Imports System.Data
Imports System.Data.Common
Imports Microsoft.Practices.EnterpriseLibrary.Data.Sql
Imports System.Web.Mvc

Namespace sample
    Class MyFoo
        Inherits Controller

        Public Sub New()
            m_db = New SqlDatabase("""")
        End Sub

        Private m_db As SqlDatabase

        Private Function GetDataBase() As SqlDatabase
            Return m_db
        End Function

        Public Sub Run(input As System.String)
            Dim db = GetDataBase()
            Dim cmd As DbCommand = db.GetSqlStringCommand(""SELECT * FROM Users WHERE username = @username and role='user'"")
            db.AddInParameter(cmd, ""@username"", DbType.String, input)
            db.ExecuteDataSet(cmd)
        End Sub
    End Class
End Namespace
";

            await VerifyCSharpDiagnostic(cSharpTest).ConfigureAwait(false);
            await VerifyVisualBasicDiagnostic(visualBasicTest).ConfigureAwait(false);
        }

        [TestMethod]
        [Ignore("Full taint analysis is needed")]
        public async Task SqlInjectionEnterpriseLibraryDataGetSqlStringCommandUnsafe()
        {
            var cSharpTest = @"
using System.Data.Common;
using Microsoft.Practices.EnterpriseLibrary.Data.Sql;
using System.Web.Mvc;

namespace sample
{
    class MyFoo : Controller
    {
        public MyFoo()
        {
            m_db = new SqlDatabase("""");
        }

        private SqlDatabase m_db;

        private SqlDatabase GetDataBase() { return m_db; }

        public void Run(string input)
        {
            var db = GetDataBase();
            DbCommand cmd = db.GetSqlStringCommand(""SELECT * FROM Users WHERE username = '"" + input + ""' and role='user'"");
            db.ExecuteDataSet(cmd);
        }
    }
}
";

            var visualBasicTest = @"
Imports System.Data.Common
Imports Microsoft.Practices.EnterpriseLibrary.Data.Sql
Imports System.Web.Mvc

Namespace sample
    Class MyFoo
        Inherits Controller

        Public Sub New()
            m_db = New SqlDatabase("""")
        End Sub

        Private m_db As SqlDatabase

        Private Function GetDataBase() As SqlDatabase
            Return m_db
        End Function

        Public Sub Run(input As System.String)
            Dim db = GetDataBase()
            Dim cmd As DbCommand = db.GetSqlStringCommand(""SELECT * FROM Users WHERE username = '"" + input + ""' and role='user'"")
            db.ExecuteDataSet(cmd)
        End Sub
    End Class
End Namespace
";

            var expected = new DiagnosticResult
            {
                Id       = "SCS0036",
                Severity = DiagnosticSeverity.Warning,
            };

            await VerifyCSharpDiagnostic(cSharpTest, expected).ConfigureAwait(false);
            await VerifyVisualBasicDiagnostic(visualBasicTest, expected).ConfigureAwait(false);
        }

        [DataRow("new SqlDataSource()", false, null)]
        [DataRow("new SqlDataSource(\"connectionString\", input)", true, "SCS0014")]
        [DataRow("new SqlDataSource(\"connectionString\", \"select\")", false, null)]
        [DataRow("new SqlDataSource(input, input)", true, "SCS0014")]
        [DataRow("new SqlDataSource(input, \"select\")", false, null)]
        [DataRow("new SqlDataSource(\"providerName\",\"connectionString\", input)", true, "SCS0014")]
        [DataRow("new SqlDataSource(input, \"connectionString\", \"select\")", false, null)]
        [DataRow("new SqlDataSource(input, input, \"select\")", false, null)]
        [DataRow("new SqlDataSource(\"providerName\", input, \"select\")", false, null)]
        [DataRow("new SqlDataAdapter()", false, null)]
        [DataRow("new SqlDataAdapter(input, new SqlConnection())", true, "SCS0026")]
        [DataRow("new SqlDataAdapter(\"select\", new SqlConnection())", false, null)]
        [DataRow("new SqlDataAdapter(input, \"connectionString\")", true, "SCS0026")]
        [DataRow("new SqlDataAdapter(\"select\", input)", false, null)]

        [DataRow("new DbContext(\"connectionString\").Set(null).SqlQuery(input, null)",          true,  "SCS0035")]
        [DataRow("new DbContext(\"connectionString\").Set(null).SqlQuery(\"select\", null)",     false, null)]
        [DataRow("new DbContext(\"connectionString\").Set<Object>().SqlQuery(input, null)",      true,  "SCS0035")]
        [DataRow("new DbContext(\"connectionString\").Set<Object>().SqlQuery(\"select\", null)", false, null)]

        [DataRow("new DbContext(\"connectionString\").Database.SqlQuery(null, input, null)",         true,  "SCS0035")]
        [DataRow("new DbContext(\"connectionString\").Database.SqlQuery(null, \"select\", null)",    false, null)]
        [DataRow("new DbContext(\"connectionString\").Database.SqlQuery<Object>(input)",             true,  "SCS0035")]
        [DataRow("new DbContext(\"connectionString\").Database.SqlQuery<Object>(\"select\", input)", false, null)]

        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommand(input, parameters)", true, "SCS0035")]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommand(\"select\", parameters)", false, null)]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, input, parameters)", true, "SCS0035")]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, \"select\", parameters)", false, null)]

        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(input, parameters)", true, "SCS0035")]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(\"select\", parameters)", false, null)]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, input, parameters)", true, "SCS0035")]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, \"select\", parameters)", false, null)]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(input, new CancellationToken(), parameters)", true, "SCS0035")]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(\"select\", new CancellationToken(), parameters)", false, null)]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, input, new CancellationToken(), parameters)", true, "SCS0035")]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, \"select\", new CancellationToken(), parameters)", false, null)]

        [DataRow("new SqlDatabase(\"connectionString\").ExecuteDataSet(CommandType.Text, input)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteDataSet(CommandType.Text, \"select\")", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteDataSet(input)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteDataSet(\"select\")", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteDataSet(input, parameters)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteDataSet(\"select\", parameters)", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteDataSet(new SqlConnection(\"\").BeginTransaction(), CommandType.Text, input)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteDataSet(new SqlConnection(\"\").BeginTransaction(), CommandType.Text, \"select\")", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteDataSet(new SqlConnection(\"\").BeginTransaction(), input, parameters)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteDataSet(new SqlConnection(\"\").BeginTransaction(), \"select\", parameters)", false, null)]

        [DataRow("new SqlDatabase(\"connectionString\").ExecuteReader(CommandType.Text, input)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteReader(CommandType.Text, \"select\")", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteReader(input)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteReader(\"select\")", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteReader(input, parameters)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteReader(\"select\", parameters)", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteReader(new SqlConnection(\"\").BeginTransaction(), CommandType.Text, input)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteReader(new SqlConnection(\"\").BeginTransaction(), CommandType.Text, \"select\")", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteReader(new SqlConnection(\"\").BeginTransaction(), input, parameters)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteReader(new SqlConnection(\"\").BeginTransaction(), \"select\", parameters)", false, null)]

        [DataRow("new SqlDatabase(\"connectionString\").ExecuteNonQuery(CommandType.Text, input)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteNonQuery(CommandType.Text, \"select\")", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteNonQuery(input)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteNonQuery(\"select\")", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteNonQuery(input, parameters)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteNonQuery(\"select\", parameters)", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteNonQuery(new SqlConnection(\"\").BeginTransaction(), CommandType.Text, input)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteNonQuery(new SqlConnection(\"\").BeginTransaction(), CommandType.Text, \"select\")", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteNonQuery(new SqlConnection(\"\").BeginTransaction(), input, parameters)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteNonQuery(new SqlConnection(\"\").BeginTransaction(), \"select\", parameters)", false, null)]

        [DataRow("new SqlDatabase(\"connectionString\").ExecuteScalar(CommandType.Text, input)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteScalar(CommandType.Text, \"select\")", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteScalar(input)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteScalar(\"select\")", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteScalar(input, parameters)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteScalar(\"select\", parameters)", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteScalar(new SqlConnection(\"\").BeginTransaction(), CommandType.Text, input)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteScalar(new SqlConnection(\"\").BeginTransaction(), CommandType.Text, \"select\")", false, null)]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteScalar(new SqlConnection(\"\").BeginTransaction(), input, parameters)", true, "SCS0036")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteScalar(new SqlConnection(\"\").BeginTransaction(), \"select\", parameters)", false, null)]

        [DataRow("new SQLiteCommand()",                                                                              false, null)]
        [DataRow("new SQLiteCommand(new SQLiteConnection())",                                                        false, null)]
        [DataRow("new SQLiteCommand(input)",                                                                         true,  "SCS0026")]
        [DataRow("new SQLiteCommand(\"select\")",                                                                    false, null)]
        [DataRow("new SQLiteCommand(input, new SQLiteConnection())",                                                 true,  "SCS0026")]
        [DataRow("new SQLiteCommand(\"select\", new SQLiteConnection())",                                            false, null)]
        [DataRow("new SQLiteCommand(input, new SQLiteConnection(), new SQLiteConnection().BeginTransaction())",      true,  "SCS0026")]
        [DataRow("new SQLiteCommand(\"select\", new SQLiteConnection(), new SQLiteConnection().BeginTransaction())", false, null)]
        [DataRow("SQLiteCommand.Execute(input, SQLiteExecuteType.Reader, CommandBehavior.Default, null)",            true,  "SCS0026")]
        [DataRow("SQLiteCommand.Execute(\"select\", SQLiteExecuteType.Reader, CommandBehavior.Default, null)",       false, null)]
        [DataRow("SQLiteCommand.Execute(input, SQLiteExecuteType.Reader, null)",                                     true,  "SCS0026")]
        [DataRow("SQLiteCommand.Execute(\"select\", SQLiteExecuteType.Reader, null)",                                false, null)]

        // Tests below are covered by SCS0026
        [DataRow("new SqlDataAdapter(new SqlCommand(input))", true, "SCS0026")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteDataSet(new SqlCommand(input))", true, "SCS0026")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteDataSet(new SqlCommand(input), new SqlConnection(\"\").BeginTransaction())", true, "SCS0026")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteReader(new SqlCommand(input))", true, "SCS0026")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteReader(new SqlCommand(input), new SqlConnection(\"\").BeginTransaction())", true, "SCS0026")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteNonQuery(new SqlCommand(input))", true, "SCS0026")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteNonQuery(new SqlCommand(input), new SqlConnection(\"\").BeginTransaction())", true, "SCS0026")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteScalar(new SqlCommand(input))", true, "SCS0026")]
        [DataRow("new SqlDatabase(\"connectionString\").ExecuteScalar(new SqlCommand(input), new SqlConnection(\"\").BeginTransaction())", true, "SCS0026")]

        [DataTestMethod]
        public async Task SqlInjection(string sink, bool warn, string warningId)
        {
            var cSharpTest = $@"
#pragma warning disable 8019
    using System;
    using System.Data.SqlClient;
    using System.Data.Common;
    using System.Data;
    using System.Web.UI.WebControls;
    using System.Data.Entity;
    using System.Threading;
    using Microsoft.Practices.EnterpriseLibrary.Data.Sql;
    using System.Data.SQLite;
    using System.Web.Mvc;
#pragma warning restore 8019

namespace sample
{{
    class MyFoo : Controller
    {{
        public void Run(string input, params object[] parameters)
        {{
            {sink};
        }}
    }}
}}
";

            sink = sink.CSharpReplaceToVBasic();

            var visualBasicTest = $@"
#Disable Warning BC50001
    Imports System
    Imports System.Data.SqlClient
    Imports System.Data.Common
    Imports System.Data
    Imports System.Web.UI.WebControls
    Imports System.Data.Entity
    Imports System.Threading
    Imports Microsoft.Practices.EnterpriseLibrary.Data.Sql
    Imports System.Data.SQLite
    Imports System.Web.Mvc
#Enable Warning BC50001

Namespace sample
    Class MyFoo
        Inherits Controller

        Public Sub Run(input As System.String, ParamArray parameters() As Object)
            Dim temp = {sink}
        End Sub
    End Class
End Namespace
";
            var expected = new DiagnosticResult
            {
                Id = warningId,
                Severity = DiagnosticSeverity.Warning,
            };

            if (warn)
            {
                await VerifyCSharpDiagnostic(cSharpTest, expected).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(visualBasicTest, expected).ConfigureAwait(false);
            }
            else
            {
                await VerifyCSharpDiagnostic(cSharpTest).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(visualBasicTest).ConfigureAwait(false);
            }
        }

        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(input, parameters)", true, "SCS0035")]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(\"select\", parameters)", false, null)]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, input, parameters)", true, "SCS0035")]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, \"select\", parameters)", false, null)]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(input, new CancellationToken(), parameters)", true, "SCS0035")]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(\"select\", new CancellationToken(), parameters)", false, null)]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, input, new CancellationToken(), parameters)", true, "SCS0035")]
        [DataRow("new DbContext(\"connectionString\").Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, \"select\", new CancellationToken(), parameters)", false, null)]
        [DataTestMethod]
        public async Task AwaitedSqlInjection(string sink, bool warn, string warningId)
        {
            var cSharpTest = $@"
#pragma warning disable 8019
    using System;
    using System.Threading.Tasks;
    using System.Data.SqlClient;
    using System.Data.Common;
    using System.Data;
    using System.Web.UI.WebControls;
    using System.Data.Entity;
    using System.Threading;
    using Microsoft.Practices.EnterpriseLibrary.Data.Sql;
    using System.Data.SQLite;
    using System.Web.Mvc;
#pragma warning restore 8019

namespace sample
{{
    class MyFoo : Controller
    {{
        public async Task Run(string input, params object[] parameters)
        {{
            await {sink};
        }}
    }}
}}
";

            sink = sink.CSharpReplaceToVBasic();

            var visualBasicTest = $@"
#Disable Warning BC50001
    Imports System
    Imports System.Data.SqlClient
    Imports System.Data.Common
    Imports System.Data
    Imports System.Web.UI.WebControls
    Imports System.Data.Entity
    Imports System.Threading
    Imports Microsoft.Practices.EnterpriseLibrary.Data.Sql
    Imports System.Data.SQLite
    Imports System.Web.Mvc
#Enable Warning BC50001

Namespace sample
    Class MyFoo
        Inherits Controller

        Public Async Sub Run(input As System.String, ParamArray parameters() As Object)
            Dim temp = Await {sink}
        End Sub
    End Class
End Namespace
";
            var expected = new DiagnosticResult
            {
                Id = warningId,
                Severity = DiagnosticSeverity.Warning,
            };

            if (warn)
            {
                await VerifyCSharpDiagnostic(cSharpTest, expected).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(visualBasicTest, expected).ConfigureAwait(false);
            }
            else
            {
                await VerifyCSharpDiagnostic(cSharpTest).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(visualBasicTest).ConfigureAwait(false);
            }
        }

        // todo: EF Core 2.2, 3.1
        [DataRow("new SampleContext().Test.FromSql(input)", true)]
        [DataRow("new SampleContext().Test.FromSql(input, null)", true)]
        [DataRow("new SampleContext().Test.FromSql(\"select\")", false)]
        [DataRow("new SampleContext().Test.FromSql(\"select\", null)", false)]
        [DataRow("new SampleContext().Test.FromSql(\"select {0}\", input)", false)]
        [DataRow("new SampleContext().Database.ExecuteSqlCommand(input)", true)]
        [DataRow("new SampleContext().Database.ExecuteSqlCommand(input, null)", true)]
        [DataRow("new SampleContext().Database.ExecuteSqlCommand(\"select\")", false)]
        [DataRow("new SampleContext().Database.ExecuteSqlCommand(\"select\", null)", false)]
        [DataRow("new SampleContext().Database.ExecuteSqlCommand(\"select {0}\", input)", false)]
        [DataTestMethod]
        public async Task SqlInjectionEntityFrameworkCore(string sink, bool warn)
        {
            var cSharpTest = $@"
using Microsoft.EntityFrameworkCore;
using System.Web.Mvc;

namespace sample
{{
    public class SampleContext : DbContext
    {{
        public DbSet<string> Test {{ get; set; }}
    }}

    class MyFoo : Controller
    {{
        public void Run(string input, params object[] parameters)
        {{
            {sink};
        }}
    }}
}}
";

            sink = sink.CSharpReplaceToVBasic();

            var visualBasicTest = $@"
Imports Microsoft.EntityFrameworkCore
Imports System.Web.Mvc

Namespace sample
    Public Class SampleContext
        Inherits DbContext

        Public Property Test As DbSet(Of String)
    End Class

    Class MyFoo
        Inherits Controller

        Public Sub Run(input As System.String, ParamArray parameters() As Object)
            Dim temp = {sink}
        End Sub
    End Class
End Namespace
";
            var expected = new DiagnosticResult
            {
                Id = "SCS0035",
                Severity = DiagnosticSeverity.Warning,
            };

            if (warn)
            {
                await VerifyCSharpDiagnostic(cSharpTest, expected).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(visualBasicTest, expected).ConfigureAwait(false);
            }
            else
            {
                await VerifyCSharpDiagnostic(cSharpTest).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(visualBasicTest).ConfigureAwait(false);
            }
        }

        // todo: 2.0
        [DataRow("new SqliteCommand()",                                                                              false)]
        [DataRow("new SqliteCommand(input)",                                                                         true)]
        [DataRow("new SqliteCommand(\"select\")",                                                                    false)]
        [DataRow("new SqliteCommand(input, null)",                                                                   true)]
        [DataRow("new SqliteCommand(\"select\", new SqliteConnection())",                                            false)]
        [DataRow("new SqliteCommand(input, null, null)",                                                             true)]
        [DataRow("new SqliteCommand(\"select\", new SqliteConnection(), new SqliteConnection().BeginTransaction())", false)]
        [DataTestMethod]
        public async Task MicrosoftSqlite(string sink, bool warn)
        {
            var cSharpTest = $@"
using Microsoft.Data.Sqlite;
using System.Web.Mvc;

namespace sample
{{
    class MyFoo : Controller
    {{
        public void Run(string input, params object[] parameters)
        {{
            {sink};
        }}
    }}
}}
";

            sink = sink.CSharpReplaceToVBasic();

            var visualBasicTest = $@"
Imports Microsoft.Data.Sqlite
Imports System.Web.Mvc

Namespace sample
    Class MyFoo
        Inherits Controller

        Public Sub Run(input As System.String, ParamArray parameters() As Object)
            Dim temp = {sink}
        End Sub
    End Class
End Namespace
";
            var expected = new DiagnosticResult
            {
                Id = "SCS0026",
                Severity = DiagnosticSeverity.Warning,
            };

            if (warn)
            {
                await VerifyCSharpDiagnostic(cSharpTest, expected).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(visualBasicTest, expected).ConfigureAwait(false);
            }
            else
            {
                await VerifyCSharpDiagnostic(cSharpTest).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(visualBasicTest).ConfigureAwait(false);
            }
        }

        [DataRow("\"SELECT * FROM Users WHERE username = '\" + username + \"';\"", true)]
        [DataRow("\"SELECT * FROM Users WHERE username = 'indy@email.com';\"", false)]
        [DataTestMethod]
        public async Task NHibernateSqlInjection(string sink, bool warn)
        {
            var testConfig = @"
TaintEntryPoints:
  AAA:
    Namespace: Foo
    ClassName: SampleClass
    Name: Execute
";

            var optionsWithProjectConfig = ConfigurationTest.CreateAnalyzersOptionsWithConfig(testConfig);

            var cSharpTest = $@"
using NHibernate;

namespace Foo
{{
    public class SampleClass
    {{
        private ISession session = null;

        public void Execute(string username)
        {{
            session.CreateSQLQuery({sink});
        }}
    }}
}}
";

            var visualBasicTest = $@"
Imports NHibernate

Namespace Foo
    Public Class SampleClass
        Private session As ISession = Nothing

        Public Sub Execute(ByVal username As String)
            session.CreateSQLQuery({sink})
        End Sub
    End Class
End Namespace
";

            var expected = new DiagnosticResult
            {
                Id       = "SCS0037",
                Severity = DiagnosticSeverity.Warning,
            };

            if (warn)
            {
                await VerifyCSharpDiagnostic(cSharpTest, expected, optionsWithProjectConfig).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(visualBasicTest, expected, optionsWithProjectConfig).ConfigureAwait(false);
            }
            else
            {
                await VerifyCSharpDiagnostic(cSharpTest, options: optionsWithProjectConfig).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(visualBasicTest, options: optionsWithProjectConfig).ConfigureAwait(false);
            }
        }

        [DataRow("\"SELECT * FROM Users WHERE username = '\" + username + \"';\"",                           true)]
        [DataRow("\"SELECT * FROM Users WHERE username = '\" + username + \"';\", 1",                        true)]
        [DataRow("\"SELECT * FROM Users WHERE username = '\" + username + \"';\", ConsistencyLevel.All",     true)]

        [DataRow("\"SELECT * FROM Users WHERE username = 'indy@email.com';\"",                           false)]
        [DataRow("\"SELECT * FROM Users WHERE username = 'indy@email.com';\", 1",                        false)]
        [DataRow("\"SELECT * FROM Users WHERE username = 'indy@email.com';\", ConsistencyLevel.All",     false)]
        [DataTestMethod]
        public async Task CassandraCqlInjection(string sink, bool warn)
        {
            var testConfig = @"
TaintEntryPoints:
  AAA:
    Namespace: Foo
    ClassName: SampleClass
    Name: Execute
";

            var optionsWithProjectConfig = ConfigurationTest.CreateAnalyzersOptionsWithConfig(testConfig);

            var cSharpTest = $@"
using Cassandra;

namespace Foo
{{
    public class SampleClass
    {{
        private ISession session = null;

        public void Execute(string username)
        {{
            session.Execute({sink});
        }}
    }}
}}
";

            var visualBasicTest = $@"
Imports Cassandra

Namespace Foo
    Public Class SampleClass
        Private session As ISession = Nothing

        Public Sub Execute(ByVal username As String)
            session.Execute({sink})
        End Sub
    End Class
End Namespace
";
            var expected = new DiagnosticResult
            {
                Id       = "SCS0038",
                Severity = DiagnosticSeverity.Warning,
            };

            if (warn)
            {
                await VerifyCSharpDiagnostic(cSharpTest, expected, optionsWithProjectConfig).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(visualBasicTest, expected, optionsWithProjectConfig).ConfigureAwait(false);
            }
            else
            {
                await VerifyCSharpDiagnostic(cSharpTest, options: optionsWithProjectConfig).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(visualBasicTest, options: optionsWithProjectConfig).ConfigureAwait(false);
            }
        }

        [DataRow("var sql = new NpgsqlCommand(\"SELECT * FROM users WHERE username = '\" + username + \"';\");",                  true)]
        [DataRow("var sql = new NpgsqlCommand(\"SELECT * FROM users WHERE username = '\" + username + \"';\", null);",            true)]
        [DataRow("var sql = new NpgsqlCommand(\"SELECT * FROM users WHERE username = '\" + username + \"';\", null, null);",      true)]
        [DataRow("var sql = new NpgsqlCommand(); sql.CommandText = \"SELECT * FROM users WHERE username = '\" + username + \"';\";", true)]

        [DataRow("var sql = new NpgsqlCommand(\"SELECT * FROM users WHERE username = 'indy@email.com';\");",                  false)]
        [DataRow("var sql = new NpgsqlCommand(\"SELECT * FROM users WHERE username = 'indy@email.com';\", null);",            false)]
        [DataRow("var sql = new NpgsqlCommand(\"SELECT * FROM users WHERE username = 'indy@email.com';\", null, null);",      false)]
        [DataRow("var sql = new NpgsqlCommand(); sql.CommandText = \"SELECT * FROM users WHERE username = 'indy@email.com';\";", false)]
        [DataTestMethod]
        public async Task NpgsqlInjection(string sink, bool warn)
        {
            var testConfig = @"
TaintEntryPoints:
  AAA:
    Namespace: Foo
    ClassName: SampleClass
    Name: Execute
";

            var optionsWithProjectConfig = ConfigurationTest.CreateAnalyzersOptionsWithConfig(testConfig);

            var cSharpTest = $@"
using Npgsql;

namespace Foo
{{
    public class SampleClass
    {{
        public void Execute(string username)
        {{
            {sink}
        }}
    }}
}}
";

            sink = sink.Replace("var ", "Dim ");
            sink = sink.Replace(";", "\r\n");
            sink = sink.Replace("null", "Nothing");

            var visualBasicTest = $@"
Imports Npgsql

Namespace Foo
    Public Class SampleClass
        Public Sub Execute(ByVal username As String)
            {sink}
        End Sub
    End Class
End Namespace
";
            var expected = new DiagnosticResult
            {
                Id       = "SCS0039",
                Severity = DiagnosticSeverity.Warning,
            };

            if (warn)
            {
                await VerifyCSharpDiagnostic(cSharpTest, expected, optionsWithProjectConfig).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(visualBasicTest, expected, optionsWithProjectConfig).ConfigureAwait(false);
            }
            else
            {
                await VerifyCSharpDiagnostic(cSharpTest, options: optionsWithProjectConfig).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(visualBasicTest, options: optionsWithProjectConfig).ConfigureAwait(false);
            }
        }
    }
}
