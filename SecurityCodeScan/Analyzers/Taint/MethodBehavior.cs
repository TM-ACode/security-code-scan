﻿namespace SecurityCodeScan.Analyzers.Taint
{
    public class MethodBehavior
    {
        public int[]  InjectablesArguments { get; }
        public int[]  PasswordArguments    { get; }
        public int[]  TaintFromArguments   { get; }
        public string LocaleInjection      { get; }
        public string LocalePassword       { get; }
        public bool   IsInjectableField    { get; }
        public bool   IsPasswordField      { get; }

        public MethodBehavior(int[] injectablesArguments,
                              int[] passwordArguments,
                              int[] taintFromArguments,
                              string localeInjection,
                              string localePassword,
                              bool isInjectableField,
                              bool isPasswordField)
        {
            InjectablesArguments = injectablesArguments ?? new int[0];
            PasswordArguments    = passwordArguments ?? new int[0];
            TaintFromArguments   = taintFromArguments ?? new int[0];
            LocaleInjection      = localeInjection;
            LocalePassword       = localePassword;
            IsInjectableField    = isInjectableField;
            IsPasswordField      = isPasswordField;
        }
    }
}
