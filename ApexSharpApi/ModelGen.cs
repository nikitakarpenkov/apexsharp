﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ApexSharpApi.Model.RestApi;
using Newtonsoft.Json;
using Serilog;

namespace ApexSharpApi
{
    public class ModelGen
    {
        public const int IdStringLength = 18;

        public IEnumerable<string> GetAllObjectNames()
        {
            var objectList = new List<string>();

            var httpManager = new HttpManager();
            var requestJson = httpManager.Get("sobjects/");

            var cacheLocation = Path.Combine(ApexSharp.GetSession().VsProjectLocation, "Cache", "objectList.json");
            File.WriteAllText(cacheLocation, requestJson);
            var json = File.ReadAllText(cacheLocation);

            var sObjectList = JsonConvert.DeserializeObject<SObjectDescribe>(json);
            foreach (var sobject in sObjectList.sobjects)
            {
                objectList.Add(sobject.name);
            }

            return objectList;
        }

        private static SObjectDetail LoadSObjectImpl(string sobject)
        {
            var httpManager = new HttpManager();
            var objectDetailjson = httpManager.Get($"sobjects/{sobject}/describe");

            var sObjectDetail = JsonConvert.DeserializeObject<SObjectDetail>(objectDetailjson);

            objectDetailjson = JsonConvert.SerializeObject(sObjectDetail, Formatting.Indented);
            var jsonFileName = sobject + ".json";
            var cacheLocation = Path.Combine(ApexSharp.GetSession().VsProjectLocation, "Cache", jsonFileName);

            File.WriteAllText(cacheLocation, objectDetailjson);
            return sObjectDetail;
        }

        private static void SaveSObjectImpl(string sobject, string sObjectClass)
        {
            var saveFileName = sobject + ".cs";
            var sobjectLocation = Path.Combine(ApexSharp.GetSession().VsProjectLocation, "SObjects", saveFileName);

            File.WriteAllText(sobjectLocation, sObjectClass);

            Log.ForContext<ModelGen>().Debug("Saved {sobject}", saveFileName);
        }

        internal Func<string, SObjectDetail> LoadSObject { get; set; } = LoadSObjectImpl;

        internal Action<string, string> SaveSObject { get; set; } = SaveSObjectImpl;

        public void CreateOfflineSymbolTableForSql(List<string> sobjectList, string nameSpace,bool recursive = true, List<string> ignoreList = null)
        {
            CreateOfflineSymbolTableCore(sobjectList, nameSpace, true, recursive, ignoreList);
        }

        public void CreateOfflineSymbolTable(List<string> sobjectList, string nameSpace, bool recursive = true, List<string> ignoreList = null)
        {
            CreateOfflineSymbolTableCore(sobjectList, nameSpace, false, recursive, ignoreList);
        }

        internal void CreateOfflineSymbolTableCore(List<string> sobjectList, string nameSpace, bool orm, bool recursive = true, List<string> ignoreList = null)
        {
            // populate the list of pending objects
            var ignoreCase = StringComparer.InvariantCultureIgnoreCase;
            var processedObjects = new HashSet<string>(ignoreCase);
            var pendingObjects = new ConcurrentSet<string>(ignoreCase);
            pendingObjects.UnionWith(sobjectList);

            // add "Address" class to the list of ignored objects by default
            var ignoredObjects = new HashSet<string>(ignoreCase);
            ignoredObjects.UnionWith(ignoreList ?? new List<string> { "Address" });

            // process pending object
            while (pendingObjects.Any())
            {
                // mark current pending objects as processed
                var currentList = pendingObjects.ToList();
                processedObjects.UnionWith(currentList);
                pendingObjects.Clear();

                // load and generate pending objects
                Parallel.ForEach(currentList, sobject =>
                {
                    var sObjectDetail = LoadSObject(sobject);
                    var sObjectClass = CreateSalesForceClass(nameSpace, sObjectDetail, orm);
                    SaveSObject(sobject, sObjectClass);

                    // schedule the referenced objects
                    var references = GetReferencedSObjects(sObjectDetail).ToArray();
                    if (recursive && references.Any())
                    {
                        foreach (var refObject in references)
                            if (!processedObjects.Contains(refObject) && !ignoredObjects.Contains(refObject))
                                pendingObjects.Add(refObject);
                    }
                });
            }
        }

        internal List<string> GetReferencedSObjects(SObjectDetail sobject)
        {
            var refs =
                from field in sobject.fields
                where field.type == ReferenceType &&
                    field.referenceTo != null &&
                    field.referenceTo.Any() &&
                    !string.IsNullOrWhiteSpace(field.relationshipName)
                select field.referenceTo[0];

            return refs.Distinct().ToList();
        }

        internal string CreateSalesForceClass(string nameSpace, SObjectDetail objectDetail, bool orm = false)
        {
            var sb = new StringBuilder();

            sb.AppendLine("namespace " + nameSpace);
            sb.AppendLine("{");
            sb.AppendLine("\tusing Apex.System;");
            sb.AppendLine("\tusing ApexSharpApi.ApexApi;");
            sb.AppendLine("\tusing ApexSharpApi.Attributes;");
            if (orm)
            {
                sb.AppendLine("\tusing ServiceStack.DataAnnotations;");
            }

            sb.AppendLine("\tusing DateTime = global::System.DateTime;");
            sb.AppendLine();
            sb.AppendLine($"\tpublic class {objectDetail.name} : SObject");
            sb.AppendLine("\t{");

            var setGet = "{set;get;}";

            // Add a different name for ID if we are going to use SF Id as SF Id is different between systems
            if (orm)
            {
                sb.AppendLine($"\t\t[PrimaryKey]");
                sb.AppendLine($"\t\t[AutoIncrement]");
            }

            sb.AppendLine($"\t\tpublic int ExternalId {setGet}");

            foreach (var objectField in objectDetail.fields)
            {
                if ((objectField.type == ReferenceType) && (objectField.name == "OwnerId") && (objectField.referenceTo.Length > 1))
                {
                    AddApexIdAttribute(sb, objectField.relationshipName, orm);
                    AddStringLengthAttribute(sb, IdStringLength, orm);
                    AddIgnoreUpdateAttribute(sb, objectField.createable);
                    sb.AppendLine($"\t\tpublic string {objectField.name} {setGet}");

                    AddIgnoreAttribute(sb, orm);
                    sb.AppendLine($"\t\tpublic {objectField.referenceTo[1]} {objectField.relationshipName} {setGet}");
                }
                else if (objectField.type == ReferenceType && objectField.referenceTo.Length > 0)
                {
                    AddApexIdAttribute(sb, objectField.relationshipName, orm);
                    AddStringLengthAttribute(sb, IdStringLength, orm);
                    AddIgnoreUpdateAttribute(sb, objectField.createable);
                    sb.AppendLine($"\t\tpublic string {objectField.name} {setGet}");

                    if (objectField.relationshipName != null)
                    {
                        AddIgnoreAttribute(sb, orm);
                        sb.AppendLine($"\t\tpublic {objectField.referenceTo[0]} {objectField.relationshipName} {setGet}");
                    }
                }
                else if (objectField.type != "id")
                {
                    AddStringLengthAttribute(sb, objectField, orm);
                    AddIgnoreUpdateAttribute(sb, objectField.createable);
                    sb.AppendLine($"\t\tpublic {GetFieldType(objectField, objectDetail.name)} {objectField.name} {setGet}");
                }
            }

            sb.AppendLine("\t}");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private void AddIgnoreUpdateAttribute(StringBuilder sb, bool createable)
        {
            if (!createable)
            {
                sb.AppendLine($"\t\t[IgnoreUpdate]");
            }
        }

        private void AddApexIdAttribute(StringBuilder sb, string referencePropertyName, bool orm)
        {
            if (orm && referencePropertyName != null)
            {
                sb.AppendLine($"\t\t[ApexId(\"{referencePropertyName}\")]");
            }
        }

        private static void AddIgnoreAttribute(StringBuilder sb, bool orm)
        {
            if (orm)
            {
                sb.AppendLine("\t\t[Ignore]");
            }
        }

        private void AddStringLengthAttribute(StringBuilder sb, Field salesforceField, bool orm)
        {
            if (GetFieldType(salesforceField) == "string" && salesforceField.length > 0)
            {
                AddStringLengthAttribute(sb, salesforceField.length, orm);
            }
        }

        private void AddStringLengthAttribute(StringBuilder sb, int length, bool orm)
        {
            if (orm)
            {
                sb.AppendLine($"\t\t[StringLength({length})]");
            }
        }

        internal string GetFieldType(Field salesForceField, string objectName = "Not specified")
        {
            var valueFound = FieldDictionary.TryGetValue(salesForceField.type, out var value);
            if (valueFound)
            {
                return value;
            }
            Log.Logger.Fatal($"ObjectName: {objectName} Field Type: {salesForceField.type} Field Name : {salesForceField.name} Field Length: {salesForceField.length}");
            return "string";
        }

        public List<Sobject> GetAllObjects()
        {
            string objectListPath = Path.Combine(ApexSharp.GetSession().VsProjectLocation, "objectList.json");

            HttpManager httpManager = new HttpManager();
            var requestJson = httpManager.Get("sobjects/");
            File.WriteAllText(objectListPath, requestJson);

            var json = File.ReadAllText(objectListPath);
            SObjectDescribe sObjectList = JsonConvert.DeserializeObject<SObjectDescribe>(json);
            return sObjectList.sobjects.ToList();
        }



        private const string ReferenceType = "reference";

        private static readonly Dictionary<string, string> FieldDictionary = new Dictionary<string, string>
        {
            { ReferenceType, "string" },
            {"address", "Address" },
            {"id","ID" },
            {"string","string" },
            {"picklist","string" },
            {"email","string" },
            {"textarea","string" },
            {"phone","string" },
            {"url","string" },
            {"combobox","string" },
            {"multipicklist","string" },
            {"anytype","object" },
            {"location","string" },
            {"boolean","bool" },
            {"datetime","DateTime" },
            {"time","DateTime" },
            {"date","DateTime" },
            {"currency","double" },
            {"percent","double" },
            {"double","double" },
            {"int","int" },
            {"anyType", "object" },
            {"base64", "string" },
            {"complexvalue", "string" }
        };
    }
}