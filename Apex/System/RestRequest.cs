namespace Apex.System
{
    using ApexSharp;
    using ApexSharp.ApexAttributes;
    using ApexSharp.Implementation;
    using global::Apex.System;

    /// <summary>
    /// https://developer.salesforce.com/docs/atlas.en-us.apexcode.meta/apexcode/apex_methods_system_restrequest.htm#apex_methods_system_restrequest
    /// </summary>
    public class RestRequest
    {
        // infrastructure
        public RestRequest(dynamic self)
        {
            Self = self;
        }

        dynamic Self { get; set; }

        static dynamic Implementation
        {
            get
            {
                return Implementor.GetImplementation(typeof(RestRequest));
            }
        }

        // API
        public RestRequest()
        {
            Self = Implementation.Constructor();
        }

        public Map<string, string> headers
        {
            get
            {
                return Self.headers;
            }
            set
            {
                Self.headers = value;
            }
        }

        public string httpMethod
        {
            get
            {
                return Self.httpMethod;
            }
            set
            {
                Self.httpMethod = value;
            }
        }

        public Map<string, string> @params
        {
            get
            {
                return Self.@params;
            }
            set
            {
                Self.@params = value;
            }
        }

        public string remoteAddress
        {
            get
            {
                return Self.remoteAddress;
            }
            set
            {
                Self.remoteAddress = value;
            }
        }

        public Blob requestBody
        {
            get
            {
                return Self.requestBody;
            }
            set
            {
                Self.requestBody = value;
            }
        }

        public string requestURI
        {
            get
            {
                return Self.requestURI;
            }
            set
            {
                Self.requestURI = value;
            }
        }

        public string resourcePath
        {
            get
            {
                return Self.resourcePath;
            }
            set
            {
                Self.resourcePath = value;
            }
        }

        public void addHeader(string name, string value)
        {
            Self.addHeader(name, value);
        }

        public void addParameter(string name, string value)
        {
            Self.addParameter(name, value);
        }

        public object clone()
        {
            return Self.clone();
        }
    }
}
