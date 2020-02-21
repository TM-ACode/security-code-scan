﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SecurityCodeScan.Analyzers;
using SecurityCodeScan.Analyzers.Taint;
using SecurityCodeScan.Test.Config;
using SecurityCodeScan.Test.Helpers;
using DiagnosticVerifier = SecurityCodeScan.Test.Helpers.DiagnosticVerifier;

namespace SecurityCodeScan.Test.Taint
{
    [TestClass]
    public class OpenRedirectAnalyzerTest : DiagnosticVerifier
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
            MetadataReference.CreateFromFile(typeof(System.Web.HttpResponse).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Web.Mvc.ActionResult).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Http.HttpResponse).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Mvc.Controller).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Mvc.ControllerBase).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Mvc.IUrlHelper).Assembly.Location),
        };

        private DiagnosticResult Expected = new DiagnosticResult
        {
            Id       = "SCS0027",
            Severity = DiagnosticSeverity.Warning,
        };

        protected override IEnumerable<MetadataReference> GetAdditionalReferences() => References;

        [TestCategory("Detect")]
        [DataRow("System.Web",                "Response.Redirect(input)")]
        [DataRow("System.Web",                "Response.Redirect(input, true)")]
        [DataRow("System.Web",                "Response.RedirectPermanent(input)")]
        [DataRow("System.Web",                "Response.RedirectPermanent(input, true)")]
        [DataRow("Microsoft.AspNetCore.Http", "Response.Redirect(input)")]
        [DataRow("Microsoft.AspNetCore.Http", "Response.Redirect(input, true)")]
        [DataTestMethod]
        public async Task OpenRedirect(string @namespace, string sink)
        {
            var cSharpTest = $@"
using {@namespace};

class OpenRedirect
{{
    public static HttpResponse Response = null;

    public void Run(string input)
    {{
        {sink};
    }}
}}
";

            var visualBasicTest = $@"
Imports {@namespace}

Class OpenRedirect
    Public Shared Response As HttpResponse

    Public Sub Run(input As String)
        {sink}
    End Sub
End Class
";

            var testConfig = @"
TaintEntryPoints:
  AAA:
    ClassName: OpenRedirect
";

            var optionsWithProjectConfig = ConfigurationTest.CreateAnalyzersOptionsWithConfig(testConfig);
            await VerifyCSharpDiagnostic(cSharpTest, Expected, optionsWithProjectConfig).ConfigureAwait(false);
            await VerifyVisualBasicDiagnostic(visualBasicTest, Expected, optionsWithProjectConfig).ConfigureAwait(false);
        }

        [TestCategory("Safe")]
        [DataRow("System.Web",                "Response.Redirect(\"\")")]
        [DataRow("System.Web",                "Response.Redirect(\"\", flag)")]
        [DataRow("System.Web",                "Response.RedirectPermanent(\"\")")]
        [DataRow("System.Web",                "Response.RedirectPermanent(\"\", flag)")]
        [DataRow("Microsoft.AspNetCore.Http", "Response.Redirect(\"\")")]
        [DataRow("Microsoft.AspNetCore.Http", "Response.Redirect(\"\", flag)")]
        [DataTestMethod]
        public async Task OpenRedirectConst(string @namespace, string sink)
        {
            var cSharpTest = $@"
using {@namespace};

class OpenRedirect
{{
    public static HttpResponse Response = null;

    public void Run(bool flag)
    {{
        {sink};
    }}
}}
";

            var visualBasicTest = $@"
Imports {@namespace}

Class OpenRedirect
    Public Shared Response As HttpResponse

    Public Sub Run(flag As Boolean)
        {sink}
    End Sub
End Class
";

            var testConfig = @"
TaintEntryPoints:
  AAA:
    ClassName: OpenRedirect
";

            var optionsWithProjectConfig = ConfigurationTest.CreateAnalyzersOptionsWithConfig(testConfig);
            await VerifyCSharpDiagnostic(cSharpTest, null, optionsWithProjectConfig).ConfigureAwait(false);
            await VerifyVisualBasicDiagnostic(visualBasicTest, null, optionsWithProjectConfig).ConfigureAwait(false);
        }

        [TestCategory("Detect")]
        [DataRow("System.Web.Mvc",           "Redirect(input)")]
        [DataRow("System.Web.Mvc",           "RedirectPermanent(input)")]
        [DataRow("Microsoft.AspNetCore.Mvc", "Redirect(input)")]
        [DataRow("Microsoft.AspNetCore.Mvc", "RedirectPermanent(input)")]
        [DataRow("System.Web.Mvc",           "new RedirectResult(input)")]
        [DataRow("System.Web.Mvc",           "new RedirectResult(input, true)")]
        [DataRow("Microsoft.AspNetCore.Mvc", "new RedirectResult(input)")]
        [DataRow("Microsoft.AspNetCore.Mvc", "new RedirectResult(input, true)")]
        // todo: AspNetCore 2.0
        //[DataRow("Microsoft.AspNetCore.Mvc", "new RedirectResult(input, true, true)")]
        //[DataRow("Microsoft.AspNetCore.Mvc", "RedirectPreserveMethod(input)")]
        //[DataRow("Microsoft.AspNetCore.Mvc", "RedirectPermanentPreserveMethod(input)")]
        [DataTestMethod]
        public async Task OpenRedirectController(string @namespace, string sink)
        {
            var cSharpTest = $@"
using {@namespace};

class OpenRedirect : Controller
{{
    public ActionResult Run(string input)
    {{
        return {sink};
    }}
}}
";

            var visualBasicTest = $@"
Imports {@namespace}

Public Class OpenRedirect
    Inherits Controller

    Public Function Run(input As String) as ActionResult
        Return {sink}
    End Function
End Class
";

            await VerifyCSharpDiagnostic(cSharpTest, Expected).ConfigureAwait(false);
            await VerifyVisualBasicDiagnostic(visualBasicTest, Expected).ConfigureAwait(false);
        }

        [TestCategory("Safe")]
        [DataRow("System.Web.Mvc",           "Redirect(\"\")")]
        [DataRow("System.Web.Mvc",           "RedirectPermanent(\"\")")]
        [DataRow("Microsoft.AspNetCore.Mvc", "Redirect(\"\")")]
        [DataRow("Microsoft.AspNetCore.Mvc", "RedirectPermanent(\"\")")]
        [DataRow("System.Web.Mvc",           "new RedirectResult(\"\")")]
        [DataRow("System.Web.Mvc",           "new RedirectResult(\"\", flag)")]
        [DataRow("Microsoft.AspNetCore.Mvc", "new RedirectResult(\"\")")]
        [DataRow("Microsoft.AspNetCore.Mvc", "new RedirectResult(\"\", flag)")]
        // todo: AspNetCore 2.0
        //[DataRow("Microsoft.AspNetCore.Mvc", "new RedirectResult(\"\", flag, flag)")]
        //[DataRow("Microsoft.AspNetCore.Mvc", "RedirectPreserveMethod(\"\")")]
        //[DataRow("Microsoft.AspNetCore.Mvc", "RedirectPermanentPreserveMethod(\"\")")]
        [DataRow("System.Web.Mvc",           "Redirect(Url.RouteUrl(new {controller = input}) + \"#Id\")")]
        [DataTestMethod]
        public async Task OpenRedirectControllerConst(string @namespace, string sink)
        {
            var cSharpTest = $@"
using {@namespace};

class OpenRedirect : Controller
{{
    public ActionResult Run(bool flag, string input)
    {{
        return {sink};
    }}
}}
";

            sink = sink.CSharpReplaceToVBasic();
            var visualBasicTest = $@"
Imports {@namespace}

Public Class OpenRedirect
    Inherits Controller

    Public Function Run(flag As Boolean, input As System.String) as ActionResult
        Return {sink}
    End Function
End Class
";

            await VerifyCSharpDiagnostic(cSharpTest).ConfigureAwait(false);
            await VerifyVisualBasicDiagnostic(visualBasicTest).ConfigureAwait(false);
        }

        [TestCategory("Detect")]
        [DataRow("Microsoft.AspNetCore.Mvc")]
        [DataTestMethod]
        public async Task OpenRedirectController2(string @namespace)
        {
            var cSharpTest1 = $@"
using {@namespace};

class OpenRedirect : Controller
{{
    public ActionResult Run(string input)
    {{
        var a = new RedirectResult("""");
        a.Url = input;
        return a;
    }}
}}
";

            var cSharpTest2 = $@"
using {@namespace};

class OpenRedirect : Controller
{{
    public ActionResult Run(string input)
    {{
        return new RedirectResult("""") {{Url = input}};
    }}
}}
";

            var visualBasicTest = $@"
Imports {@namespace}

Public Class OpenRedirect
    Inherits Controller

    Public Function Run(input As String) as ActionResult
        Dim a As New RedirectResult("""")
        a.Url = input
        Return a
    End Function
End Class
";

            await VerifyCSharpDiagnostic(cSharpTest1, Expected).ConfigureAwait(false);
            await VerifyCSharpDiagnostic(cSharpTest2, Expected).ConfigureAwait(false);
            await VerifyVisualBasicDiagnostic(visualBasicTest, Expected).ConfigureAwait(false);
        }

        [TestCategory("Safe")]
        [DataRow("Microsoft.AspNetCore.Mvc", "Url = \"\"")]
        [DataRow("Microsoft.AspNetCore.Mvc", "Url = Url.RouteUrl(input)")]
        [DataTestMethod]
        public async Task OpenRedirectController2Const(string @namespace, string sink)
        {
            var cSharpTest1 = $@"
using {@namespace};

class OpenRedirect : Controller
{{
    public ActionResult Run(string input)
    {{
        var a = new RedirectResult("""");
        a.{sink};
        return a;
    }}
}}
";

            var cSharpTest2 = $@"
using {@namespace};

class OpenRedirect : Controller
{{
    public ActionResult Run(string input)
    {{
        return new RedirectResult("""") {{{sink}}};
    }}
}}
";

            var visualBasicTest = $@"
Imports {@namespace}

Public Class OpenRedirect
    Inherits Controller

    Public Function Run(input As String) as ActionResult
        Dim a As New RedirectResult("""")
        a.{sink}
        Return a
    End Function
End Class
";

            await VerifyCSharpDiagnostic(cSharpTest1).ConfigureAwait(false);
            await VerifyCSharpDiagnostic(cSharpTest2).ConfigureAwait(false);
            await VerifyVisualBasicDiagnostic(visualBasicTest).ConfigureAwait(false);
        }

        [DataRow("false", "string scary", "(scary, injectable: true)", true)]
        [DataRow("false", "string scary", "(scary, injectable: false)", false)]
        [DataRow("false", "string scary", "(scary)", false)]
        [DataRow("true", "string scary", "(scary)", true)]
        [DataRow("true", "", "{x = 0}", false)]
        [DataRow("false", "", "{x = 0}", false)]
        [DataTestMethod]
        public async Task ConditionalConstructorOpenRedirectCSharp(string injectableByDefault, string arguments, string parameters, bool warn)
        {
            var cSharpTest = $@"
using Microsoft.AspNetCore.Mvc;

class OpenRedirect : Controller
{{
    public ActionResult Foo({arguments})
    {{
        return new ConditionallyScaryRedirect{parameters};
    }}
}}

class ConditionallyScaryRedirect : ActionResult
{{
    public ConditionallyScaryRedirect(string maybeTainted = null, bool injectable = {injectableByDefault}) : base()
    {{
        // pretend there's something here
    }}

#pragma warning disable CS0649
    public int x;
#pragma warning restore CS0649
}}
";

            var testConfig = @"
Behavior:

  Conditional:
    ClassName: ConditionallyScaryRedirect
    Name: .ctor
    Method:
      Condition: {1: { Value: True } }
      ArgTypes: (System.String, System.Boolean)
      InjectableArguments: [SCS0027: 0]
";

            var config = ConfigurationTest.CreateAnalyzersOptionsWithConfig(testConfig);

            if (warn)
            {
                var expectedCSharp =
                new[]
                {
                    Expected.WithLocation(8)
                };

                var expectedVB =
                new[]
                {
                    Expected.WithLocation(8)
                };

                await VerifyCSharpDiagnostic(cSharpTest, expectedCSharp, options: config).ConfigureAwait(false);
            }
            else
            {
                await VerifyCSharpDiagnostic(cSharpTest, null, options: config).ConfigureAwait(false);
            }
        }

        [DataRow("False", "ByVal scary As String", "(scary, injectable:=True)",  true)]
        [DataRow("False", "ByVal scary As String", "(scary, injectable:=False)", false)]
        [DataRow("False", "ByVal scary As String", "(scary)",                    false)]
        [DataRow("True",  "ByVal scary As String", "(scary)",                    true)]
        [DataRow("True",  "",                      " With {.x = 0}",             false)]
        [DataRow("False", "",                      " With {.x = 0}",             false)]
        [DataTestMethod]
        public async Task ConditionalConstructorOpenRedirectVBasic(string injectableByDefault, string arguments, string parameters, bool warn)
        {
            var vbTest = $@"
Imports Microsoft.AspNetCore.Mvc

Class OpenRedirect
    Inherits Controller

    Public Function Foo({arguments}) As ActionResult
        Return New ConditionallyScaryRedirect{parameters}
    End Function
End Class

Class ConditionallyScaryRedirect
    Inherits ActionResult

    Public Sub New(ByVal Optional maybeTainted As String = Nothing, ByVal Optional injectable As Boolean = {injectableByDefault})
        MyBase.New()
    End Sub

    Public x As Integer
End Class
";

            var testConfig = @"
Behavior:

  Conditional:
    ClassName: ConditionallyScaryRedirect
    Name: .ctor
    Method:
      Condition: {1: { Value: True } }
      ArgTypes: (System.String, System.Boolean)
      InjectableArguments: [SCS0027: 0]
";

            var config = ConfigurationTest.CreateAnalyzersOptionsWithConfig(testConfig);

            if (warn)
            {
                var expectedCSharp =
                new[]
                {
                    Expected.WithLocation(8)
                };

                var expectedVB =
                new[]
                {
                    Expected.WithLocation(8)
                };

                await VerifyVisualBasicDiagnostic(vbTest, expectedVB, options: config).ConfigureAwait(false);
            }
            else
            {
                await VerifyVisualBasicDiagnostic(vbTest, null, options: config).ConfigureAwait(false);
            }
        }

        [TestCategory("Detect")]
        [TestMethod]
        public async Task ConditionalOpenRedirect()
        {
            var cSharpTest1 = @"
using Microsoft.AspNetCore.Mvc;

class OpenRedirect : Controller
{
    public ActionResult Vulnerable(string scary)
    {
        return ConditionalRedirect(scary, false);
    }

    public ActionResult Safe(string notScary)
    {
        return ConditionalRedirect(notScary, true);
    }

    private ActionResult ConditionalRedirect(string url, bool internalOnly)
    {
        // pretend this does something
        return null;
    }
}
";
            var cSharpTest2 = @"
using Microsoft.AspNetCore.Mvc;

class OpenRedirect : Controller
{
    public ActionResult Vulnerable(string scary1)
    {
        return ConditionalRedirect(scary1, false);
    }

    public ActionResult VulnerableNamed(string scary2)
    {
        return ConditionalRedirect(internalOnly: false, url: scary2);
    }

    public ActionResult Safe(string notScary1)
    {
        return ConditionalRedirect(notScary1);
    }

    public ActionResult SafeNamed1(string notScary2)
    {
        return ConditionalRedirect(url: notScary2);
    }

    public ActionResult SafeNamed2(string notScary3)
    {
        return ConditionalRedirect(internalOnly: true, url: notScary3);
    }

    private ActionResult ConditionalRedirect(string url, bool internalOnly = true)
    {
        // pretend this does something
        return null;
    }
}
";

            var vbTest1 = @"
Imports Microsoft.AspNetCore.Mvc

Class OpenRedirect
    Inherits Controller

    Public Function Vulnerable(ByVal scary As String) As ActionResult
        Return ConditionalRedirect(scary, False)
    End Function

    Public Function Safe(ByVal notScary As String) As ActionResult
        Return ConditionalRedirect(notScary, True)
    End Function

    Private Function ConditionalRedirect(ByVal url As String, ByVal internalOnly As Boolean) As ActionResult
        Return Nothing
    End Function
End Class
";

            var vbTest2 = @"
Imports Microsoft.AspNetCore.Mvc

Class OpenRedirect
    Inherits Controller

    Public Function Vulnerable(ByVal scary1 As String) As ActionResult
        Return ConditionalRedirect(scary1, False)
    End Function

    Public Function VulnerableNamed(ByVal scary2 As String) As ActionResult
        Return ConditionalRedirect(internalOnly:=False, url:=scary2)
    End Function

    Public Function Safe(ByVal notScary1 As String) As ActionResult
        Return ConditionalRedirect(notScary1)
    End Function

    Public Function SafeNamed1(ByVal notScary2 As String) As ActionResult
        Return ConditionalRedirect(url:=notScary2)
    End Function

    Public Function SafeNamed2(ByVal notScary3 As String) As ActionResult
        Return ConditionalRedirect(internalOnly:=True, url:=notScary3)
    End Function

    Private Function ConditionalRedirect(ByVal url As String, ByVal Optional internalOnly As Boolean = True) As ActionResult
        Return Nothing
    End Function
End Class

";


            var testConfig = @"
Behavior:

  Conditional:
    ClassName: OpenRedirect
    Name: ConditionalRedirect
    Method:
      Condition: {1: { Value: False } }
      ArgTypes: (System.String, System.Boolean)
      InjectableArguments: [SCS0027: 0]
";

            var expectedCSharp1 =
                new[]
                {
                    Expected.WithLocation(8, 36)
                };

            var expectedCSharp2 =
                new[]
                {
                    Expected.WithLocation(8, 36),
                    Expected.WithLocation(13, 62)
                };
            var expectedVB1 =
                new[]
                {
                    Expected.WithLocation(8, 36)
                };
            var expectedVB2 =
                new[]
                {
                    Expected.WithLocation(8, 36),
                    Expected.WithLocation(12, 62)
                };

            var config = ConfigurationTest.CreateAnalyzersOptionsWithConfig(testConfig);
            await VerifyCSharpDiagnostic(cSharpTest1, expectedCSharp1, options: config).ConfigureAwait(false);
            await VerifyCSharpDiagnostic(cSharpTest2, expectedCSharp2, options: config).ConfigureAwait(false);

            await VerifyVisualBasicDiagnostic(vbTest1, expectedVB1, options: config).ConfigureAwait(false);
            await VerifyVisualBasicDiagnostic(vbTest2, expectedVB2, options: config).ConfigureAwait(false);
        }

        [DataRow(@"var t = new Test();
                   t.SetVal(value);
                   Response.Redirect(t.ToString());", true)]
        [DataRow(@"var t = new Test();
                   t.SetVal(""const"");
                   Response.Redirect(t.ToString());", false)]
        [DataRow(@"var t = new Test();
                   t.SetVal(value);
                   Response.Redirect(t.GetVal());", true)]
        [DataRow(@"var t = new Test();
                   t.SetVal(""const"");
                   Response.Redirect(t.GetVal());", false)]
        [DataRow(@"var t = new Test();
                   t.SetVal(value);
                   Response.Redirect(string.Format(""{0}"", t));", true)]
        [DataRow(@"var t = new Test();
                   t.SetVal(""const"");
                   Response.Redirect(string.Format(""{0}"", t));", false)]
        [DataTestMethod]
        public async Task OpenRedirectStringObject(string payload, bool warn)
        {
            var testConfig = @"
TaintEntryPoints:
  AAA:
    ClassName: OpenRedirect
";

            var optionsWithProjectConfig = ConfigurationTest.CreateAnalyzersOptionsWithConfig(testConfig);

            var cSharpTest = $@"
using System.Web;

class Test
{{
    private string _val;

    public void SetVal(string val)
    {{
        _val = val;
    }}

    public string GetVal()
    {{
        return _val;
    }}

    public override string ToString()
    {{
        return _val;
    }}
}}

class OpenRedirect
{{
    public static HttpResponse Response = null;

    public void Run(string value)
    {{
        {payload}
    }}
}}
";

        var vbTest = $@"
Imports System.Web

Class Test
    Private _val As String

    Public Sub SetVal(ByVal val As String)
        _val = val
    End Sub

    Public Function GetVal() As String
        Return _val
    End Function

    Public Overrides Function ToString() As String
        Return _val
    End Function
End Class

Class OpenRedirect
    Public Shared Response As HttpResponse = Nothing

    Public Sub Run(value As String)
        {payload.CSharpReplaceToVBasic()}
    End Sub
End Class
";

            if (warn)
            {
                await VerifyCSharpDiagnostic(cSharpTest, Expected, optionsWithProjectConfig).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(vbTest, Expected, optionsWithProjectConfig).ConfigureAwait(false);
            }
            else
            {
                await VerifyCSharpDiagnostic(cSharpTest, null, optionsWithProjectConfig).ConfigureAwait(false);
                await VerifyVisualBasicDiagnostic(vbTest, null, optionsWithProjectConfig).ConfigureAwait(false);
            }
        }

        [TestCategory("Safe")]
        [DataRow("Response.Redirect(\"\"+value)", false, false, new [] { "System.Byte", "System.SByte", "System.Char", "System.Boolean",
                                                                         "System.Int16", "System.UInt16", "System.Int32", "System.UInt32",
                                                                         "System.Int64", "System.UInt64", "System.Single", "System.Double",
                                                                         "System.Decimal", "System.DateTime" })]

        [DataRow(@"var t = new Test();
                   t.Prop1 = value;
                   Response.Redirect(""""+t.Prop1)", false, false, new[] { "System.Byte", "System.SByte", "System.Char", "System.Boolean",
                                                                         "System.Int16", "System.UInt16", "System.Int32", "System.UInt32",
                                                                         "System.Int64", "System.UInt64", "System.Single", "System.Double",
                                                                         "System.Decimal", "System.DateTime" })]
        [DataTestMethod]
        public async Task OpenRedirectImplicitString(string sink, bool auditMode, bool warn, string[] types)
        {
            var namespaces = new[] { "System.Web", "Microsoft.AspNetCore.Http" };

            var testConfig = $@"
AuditMode: {auditMode}

TaintEntryPoints:
  AAA:
    ClassName: OpenRedirect
";

            var optionsWithProjectConfig = ConfigurationTest.CreateAnalyzersOptionsWithConfig(testConfig);

            foreach (var @namespace in namespaces)
            {
                foreach(var type in types)
                {
                    var cSharpTest = $@"
using {@namespace};

class Test
{{
    public {type} Prop1 {{ get; set; }}
}}

class OpenRedirect
{{
    public static HttpResponse Response = null;

    public void Run({type} value)
    {{
        {sink};
    }}
}}
";

                    var vbTest1 = $@"
Imports {@namespace}

Class Test
    Public Property Prop1 As {type}
End Class

Class OpenRedirect
    Public Shared Response As HttpResponse = Nothing

    Public Sub Run(value As {type})
        {CsToVbConverter.CSharpReplaceToVBasic(sink).Replace("+", "&")}
    End Sub
End Class
";

                    var vbTest2 = $@"
Imports {@namespace}

Class Test
    Public Property Prop1 As {type}
End Class

Class OpenRedirect
    Public Shared Response As HttpResponse = Nothing

    Public Sub Run(value As {type})
        {CsToVbConverter.CSharpReplaceToVBasic(sink)}
    End Sub
End Class
";

                    if (warn)
                    {
                        await VerifyCSharpDiagnostic(cSharpTest, Expected, optionsWithProjectConfig).ConfigureAwait(false);
                        await VerifyVisualBasicDiagnostic(vbTest1, Expected, optionsWithProjectConfig).ConfigureAwait(false);
                        await VerifyVisualBasicDiagnostic(vbTest2, Expected, optionsWithProjectConfig).ConfigureAwait(false);
                    }
                    else
                    {
                        await VerifyCSharpDiagnostic(cSharpTest, null, optionsWithProjectConfig).ConfigureAwait(false);
                        await VerifyVisualBasicDiagnostic(vbTest1, null, optionsWithProjectConfig).ConfigureAwait(false);
                        await VerifyVisualBasicDiagnostic(vbTest2, null, optionsWithProjectConfig).ConfigureAwait(false);
                    }
                }
            }
        }

        [TestCategory("Detect")]
        [DataRow("Response.Redirect(\"\"+value)",       new object[] { "string", "object" })]
        [DataRow("Response.Redirect(\"\"+value, flag)", new object[] { "string", "object" })]
        [DataTestMethod]
        public async Task OpenRedirectStringConcat(string sink, params string[] types)
        {
            var namespaces = new[] { "System.Web", "Microsoft.AspNetCore.Http" };

            var testConfig = @"
TaintEntryPoints:
  AAA:
    ClassName: OpenRedirect
";

            var optionsWithProjectConfig = ConfigurationTest.CreateAnalyzersOptionsWithConfig(testConfig);

            foreach (var @namespace in namespaces)
            {
                foreach (var type in types)
                {
                    var cSharpTest = $@"
using {@namespace};

class OpenRedirect
{{
    public static HttpResponse Response = null;

    public void Run({type} value, bool flag)
    {{
        {sink};
    }}
}}
";

                    var vbTest1 = $@"
Imports {@namespace}

Class OpenRedirect
    Public Shared Response As HttpResponse = Nothing

    Public Sub Run(value As {type}, flag As System.Boolean)
        {sink.Replace("+", "&")}
    End Sub
End Class
";

                    var vbTest2 = $@"
Imports {@namespace}

Class OpenRedirect
    Public Shared Response As HttpResponse = Nothing

    Public Sub Run(value As {type}, flag As System.Boolean)
        {sink}
    End Sub
End Class
";

                    await VerifyCSharpDiagnostic(cSharpTest, Expected, optionsWithProjectConfig).ConfigureAwait(false);
                    await VerifyVisualBasicDiagnostic(vbTest1, Expected, optionsWithProjectConfig).ConfigureAwait(false);
                    await VerifyVisualBasicDiagnostic(vbTest2, Expected, optionsWithProjectConfig).ConfigureAwait(false);
                }
            }
        }

        [TestCategory("Safe")]
        [DataRow("Response.Redirect($\"{value}\")", false, false, new[] { "System.Byte", "System.SByte", "System.Char", "System.Boolean",
                                                                          "System.Int16", "System.UInt16", "System.Int32", "System.UInt32",
                                                                          "System.Int64", "System.UInt64", "System.Single", "System.Double",
                                                                          "System.Decimal", "System.DateTime" })]
        [DataRow("Response.Redirect($\"{value}\")", true,  false, new[] { "System.Byte", "System.SByte", "System.Char", "System.Boolean",
                                                                          "System.Int16", "System.UInt16", "System.Int32", "System.UInt32",
                                                                          "System.Int64", "System.UInt64", "System.Single", "System.Double",
                                                                          "System.Decimal", "System.DateTime" })]

        [DataRow("Response.Redirect($\"{value:#.0}\")",             false, false, new[] { "System.Single" })] // ensure we're not broken by composite formatting
        [DataRow("Response.Redirect($\"{value:yyyy'-'MM'-'dd}\")",  false, false, new[] { "System.DateTime" })]
        [DataRow("Response.Redirect($\"{value:O}\")",               false, false, new[] { "System.DateTimeOffset" })]
        [DataRow("Response.Redirect($\"{value:G}\")",               false, false, new[] { "System.Guid" })]
        [DataRow("Response.Redirect($\"{value:#.0}\")",             true,  false, new[] { "System.Single" })]
        [DataRow("Response.Redirect($\"{value:yyyy'-'MM'-'dd}\")",  true,  false, new[] { "System.DateTime" })]
        [DataRow("Response.Redirect($\"{value:O}\")",               true,  false, new[] { "System.DateTimeOffset" })]
        [DataRow("Response.Redirect($\"{value:G}\")",               true,  false, new[] { "System.Guid" })]
        [DataTestMethod]
        public async Task OpenRedirectInterpolatedStringSafe(string sink, bool auditMode, bool warn, string[] types)
        {
            var namespaces = new[] { "System.Web", "Microsoft.AspNetCore.Http" };

            var testConfig = $@"
AuditMode: {auditMode}

TaintEntryPoints:
  AAA:
    ClassName: OpenRedirect
";

            var optionsWithProjectConfig = ConfigurationTest.CreateAnalyzersOptionsWithConfig(testConfig);

            foreach (var @namespace in namespaces)
            {
                foreach (var type in types)
                {
                    var cSharpTest = $@"
using {@namespace};

class OpenRedirect
{{
    public static HttpResponse Response = null;

    public void Run({type} value)
    {{
        {sink};
    }}
}}
";


                    var vbTest1 = $@"
Imports {@namespace}

Class OpenRedirect
    Public Shared Response As HttpResponse = Nothing

    Public Sub Run(value As {type})
        {sink.Replace("+", "&")}
    End Sub
End Class
";

                    var vbTest2 = $@"
Imports {@namespace}

Class OpenRedirect
    Public Shared Response As HttpResponse = Nothing

    Public Sub Run(value As {type})
        {sink}
    End Sub
End Class
";

                    if (warn)
                    {
                        await VerifyCSharpDiagnostic(cSharpTest, Expected, optionsWithProjectConfig).ConfigureAwait(false);
                        await VerifyVisualBasicDiagnostic(vbTest1, Expected, optionsWithProjectConfig).ConfigureAwait(false);
                        await VerifyVisualBasicDiagnostic(vbTest2, Expected, optionsWithProjectConfig).ConfigureAwait(false);
                    }
                    else
                    {
                        await VerifyCSharpDiagnostic(cSharpTest, null, optionsWithProjectConfig).ConfigureAwait(false);
                        await VerifyVisualBasicDiagnostic(vbTest1, null, optionsWithProjectConfig).ConfigureAwait(false);
                        await VerifyVisualBasicDiagnostic(vbTest2, null, optionsWithProjectConfig).ConfigureAwait(false);
                    }
                }
            }
        }

        [TestCategory("Detect")]
        [DataRow(new[] {"Response.Redirect($\"{value}\")",
                        "Response.Redirect($\"{value}\", flag)" }, new object[] { "string", "object" })]
        // we're still tainted if we use a format string
        [DataRow(new[] {"Response.Redirect($\"{value:G}\")",
                        "Response.Redirect($\"{value:G}\", flag)" }, new object[] { "string", "object" })]
        // {flag} is safe, ensure we're still tainted
        [DataRow(new[] {"Response.Redirect($\"{flag}{value}\")",
                        "Response.Redirect($\"{flag}{value}\", flag)" }, new object[] { "string", "object" })]
        // concat + interp is still tainted
        [DataRow(new[] {"Response.Redirect(flag + $\"{value}\")",
                        "Response.Redirect($\"{value}\" + flag)" }, new object[] { "string", "object" })]
        [DataTestMethod]
        public async Task OpenRedirectInterpolatedStringDetect(string[] sinks, params string[] types)
        {
            var namespaces = new[] { "System.Web", "Microsoft.AspNetCore.Http" };

            var testConfig = @"
TaintEntryPoints:
  AAA:
    ClassName: OpenRedirect
";

            var optionsWithProjectConfig = ConfigurationTest.CreateAnalyzersOptionsWithConfig(testConfig);

            foreach (var @namespace in namespaces)
            {
                foreach(var sink in sinks)
                {
                    foreach (var type in types)
                    {
                        var cSharpTest = $@"
using {@namespace};

class OpenRedirect
{{
    public static HttpResponse Response = null;

    public void Run({type} value, bool flag)
    {{
        {sink};
    }}
}}
";

                        var vbTest1 = $@"
Imports {@namespace}

Class OpenRedirect
    Public Shared Response As HttpResponse = Nothing

    Public Sub Run(value As {type}, flag As System.Boolean)
        {sink.Replace("+", "&")}
    End Sub
End Class
";

                        var vbTest2 = $@"
Imports {@namespace}

Class OpenRedirect
    Public Shared Response As HttpResponse = Nothing

    Public Sub Run(value As {type}, flag As System.Boolean)
        {sink}
    End Sub
End Class
";

                        await VerifyCSharpDiagnostic(cSharpTest, Expected, optionsWithProjectConfig).ConfigureAwait(false);
                        await VerifyVisualBasicDiagnostic(vbTest1, Expected, optionsWithProjectConfig).ConfigureAwait(false);
                        await VerifyVisualBasicDiagnostic(vbTest2, Expected, optionsWithProjectConfig).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
