namespace Apex.Reports
{
    using ApexSharp;
    using ApexSharp.ApexAttributes;
    using ApexSharp.Implementation;
    using global::Apex.System;

    /// <summary>
    ///
    /// </summary>
    public class ReportRunException : Exception
    {
        // infrastructure
        public ReportRunException(dynamic self)
        {
            Self = self;
        }

        static dynamic Implementation
        {
            get
            {
                return Implementor.GetImplementation(typeof(ReportRunException));
            }
        }

        // API
        public ReportRunException()
        {
            Self = Implementation.Constructor();
        }

        public ReportRunException(Exception param1)
        {
            Self = Implementation.Constructor(param1);
        }

        public ReportRunException(string param1, Exception param2)
        {
            Self = Implementation.Constructor(param1, param2);
        }
    }
}
