using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using log4net;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;
using Direct.Shared;
using System.Configuration;

namespace InvokeTask.Dll
{
    [DirectSealed]
    [DirectDom("Invoke Task")]
    [ParameterType(false)]
    public class TaskInvoker
    {
        private static readonly ILog logArchitect = LogManager.GetLogger(Loggers.LibraryObjects);

        [DirectDom("Invoke")]
        [DirectDomMethod("Invoke Solution ID{Text}, Workflow ID{WFID}, Priority{Number}, Input Data{Text}, Business Data{Text}")]
        [MethodDescriptionAttribute("Creates a task with the information given in the type")]
        public static string invokeTask(string solutionID, string wfID, int priority,string data,string businessData)
        {
            if (logArchitect.IsDebugEnabled)
            {
                logArchitect.DebugFormat("InvokeTask.Invoke - Started");
            }
            try
            {
                if (logArchitect.IsDebugEnabled)
                {
                    logArchitect.DebugFormat("InvokeTask.Invoke - Running");                    
                }
                return InnerInvokeTask(solutionID, wfID, priority, data, ConvertStringToList(businessData));

            }
            catch (Exception ex)
            {
                logArchitect.Error("InvokeTask.Invoke - Running - failed with Exception", ex);


            }

            logArchitect.DebugFormat("InvokeTask.Invoke - Ended");
            return "Failed to invoke, check logs for more information";

        }
        private static List<string> ConvertStringToList(string text)
        {
            List<string> parsedBusinessDataList = new List<string>();
            parsedBusinessDataList = text.Split('|').ToList();
            return parsedBusinessDataList;
        }
        private static string InnerInvokeTask(string solutionID, string wfID, int priority, string data, List<string> businessData)
        {
            string URL;
            try
            {
                URL = NiceRestHelpers.fetchFQDNfromConfig() + @"/RTServer/rest/nice/rti/ra/invocations/";
            }
            catch
            {
                URL = ConfigurationManager.AppSettings["FQDN"] + @"/RTServer/rest/nice/rti/ra/invocations/";
            }
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.MediaType = "application/json";
            request.Headers.Add("Cookie", "iPlanetDirectoryPro=" + Get_Token.getToken());

            RequestMetaData requestMetaData = new RequestMetaData();
            requestMetaData.guid = "Default";
            requestMetaData.initiatorType = "AGENT_CLIENT";
            requestMetaData.osUid = "Default";
            requestMetaData.initiatorId = Environment_Functions.ComputerName;
            requestMetaData.businessData = businessData;

            WorkflowData workflowData = new WorkflowData();
            workflowData.arguments.Add(new Argument("String", data));

            WorkflowMetaData workflowMetaData = new WorkflowMetaData();

            workflowMetaData.solution = solutionID;
            workflowMetaData.workflowPriority = priority;
            workflowMetaData.workflowId = wfID;

            RequestData requestData = new RequestData();
            requestData.workflowData = workflowData;
            requestData.workflowMetaData = workflowMetaData;

            BodyRoot bodyRoot = new BodyRoot();
            bodyRoot.requestMetaData = requestMetaData;
            bodyRoot.requestData.Add(requestData);

            string DATA = JsonConvert.SerializeObject(bodyRoot);

            using (Stream webStream = request.GetRequestStream())

            using (StreamWriter requestWriter = new StreamWriter(webStream, System.Text.Encoding.ASCII))
            {
                requestWriter.Write(DATA);
            }

            try
            {
                if (TaskInvoker.logArchitect.IsDebugEnabled)
                {
                    TaskInvoker.logArchitect.Info("Trying to create a task for SolutionID: \"" + solutionID + "\" on workflow: \"" + wfID +
                        "\" with input: " + data);
                    TaskInvoker.logArchitect.Info("Request data was:\n" + JsonConvert.SerializeObject(bodyRoot, Formatting.Indented).Replace("\"{", "{").Replace("}\"", "}").Replace(@"\", ""));
                }
                WebResponse webResponse = request.GetResponse();
                using (Stream webStream = webResponse.GetResponseStream() ?? Stream.Null)
                using (StreamReader responseReader = new StreamReader(webStream))
                {
                    string response = responseReader.ReadToEnd();
                    AnswerRoot answer = JsonConvert.DeserializeObject<AnswerRoot>(response);
                    if (TaskInvoker.logArchitect.IsDebugEnabled)
                        TaskInvoker.logArchitect.Debug("InvokeTask task ID: " + answer.requestId);
                    //return answer.message;
                    return "Success " + answer.requestId;
                }
            }
            catch (Exception e)
            {
                if (TaskInvoker.logArchitect.IsErrorEnabled)
                    TaskInvoker.logArchitect.Error("Failed to create a task for SolutionID:\n" + solutionID + "\" on workflow:\n" + workflowMetaData.workflowId +
                        "\" with input:\n" + data + ".\nReason: " + e.Message);
            }
            return "Failed to invoke, check logs for more information";
        }

        static protected string findWfID(string processName)
        {
            string URL;

            try
            {
                URL = NiceRestHelpers.fetchFQDNfromConfig() + @"/RTServer/rest/nice/rti/ra/invocations/workflows";
            }
            catch
            {
                URL = ConfigurationManager.AppSettings["FQDN"] + @"/RTServer/rest/nice/rti/ra/invocations/workflows";
            }
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);

            request.Method = "GET";
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.MediaType = "application/json";
            request.Headers.Add("Cookie", "iPlanetDirectoryPro=" + Get_Token.getToken());
            try
            {
                WebResponse webResponse = request.GetResponse();
                using (Stream webStream = webResponse.GetResponseStream() ?? Stream.Null)
                using (StreamReader responseReader = new StreamReader(webStream))
                {
                    string response = responseReader.ReadToEnd();
                    WFsAnswerRoot returnedAnswer = JsonConvert.DeserializeObject<WFsAnswerRoot>(response);
                    foreach (Workflow wf in returnedAnswer.workflows)
                    {
                        if (wf.solutionDisplay == processName)
                            return wf.id;
                    }
                    return "Workflow Not Found";
                }
            }
            catch (Exception e)
            {
            }
            return "Workflow Not Found. Please check log for more details";
        }

        #region Invoke Task Support Classes
        protected class RequestMetaData
        {
            public string initiatorType { get; set; }
            public string initiatorId { get; set; }
            public string guid { get; set; }
            public string osUid { get; set; }
            public string externalInvokerReqId { get; set; }
            public List<string> businessData { get; set; }

            public RequestMetaData()
            {
                businessData = new List<string>();
            }
        }

        protected class WorkflowMetaData
        {
            public string solution { get; set; }
            public string workflowId { get; set; }
            public int workflowPriority { get; set; }
        }

        protected class Argument
        {
            public string type { get; set; }
            public string value { get; set; }

            public Argument(string _type, string _value)
            {
                type = _type;
                value = _value;
            }
        }

        protected class WorkflowData
        {
            public List<Argument> arguments { get; set; }

            public WorkflowData()
            {
                arguments = new List<Argument>();
            }
        }

        protected class RequestData
        {
            public WorkflowMetaData workflowMetaData { get; set; }
            public WorkflowData workflowData { get; set; }
        }

        protected class BodyRoot
        {
            public RequestMetaData requestMetaData { get; set; }
            public List<RequestData> requestData { get; set; }

            public BodyRoot()
            {
                requestData = new List<RequestData>();
            }
        }

        protected class AnswerRoot
        {
            public string message { get; set; }
            public string requestId { get; set; }
        }
        #endregion

        #region Find WF ID Support Classes
        protected class Parameter
        {
            public string name { get; set; }
            public string displayName { get; set; }
            public string type { get; set; }
            public string description { get; set; }
            public bool list { get; set; }
        }

        protected class Parameters
        {
            public List<Parameter> parameters { get; set; }
        }

        protected class Definition
        {
            public string name { get; set; }
            public string displayName { get; set; }
            public string returnValueType { get; set; }
            public object description { get; set; }
            public Parameters parameters { get; set; }
        }

        protected class Workflow
        {
            public string id { get; set; }
            public Definition definition { get; set; }
            public string solutionId { get; set; }
            public string solutionDisplay { get; set; }
            public int version { get; set; }
        }

        protected class WFsAnswerRoot
        {
            public List<Workflow> workflows { get; set; }
        }
        #endregion
    }
    public class NiceRestHelpers
    {
        public static string fetchFQDNfromConfig()
        {
            string fileContent = System.IO.File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Nice_Systems\Real-Time\RTClient.exe.config");

            string parsedContent = fileContent.Substring(fileContent.IndexOf("syncInvocationServiceUrl") + "syncInvocationServiceUrl=\"".Length);

            return parsedContent.Substring(0, parsedContent.IndexOf("/RTServer"));
        }
    }
    class Get_Token
    {

        public static string getToken()
        {
            string URL;
            try
            {
                URL = NiceRestHelpers.fetchFQDNfromConfig() + @"/openam/json/authenticate";
            }
            catch
            {
                URL = ConfigurationManager.AppSettings["FQDN"] + @"/openam/json/authenticate";
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);
            request.Method = "POST";
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.MediaType = "application/json";
            request.PreAuthenticate = true;
            request.AllowAutoRedirect = true;
            request.Headers.Add("X-OpenAM-Username", "commonground");
            request.Headers.Add("X-OpenAM-Password", "y4Ze%NFvgExgS^A2PF!R");

            try
            {
                WebResponse webResponse = request.GetResponse();
                using (Stream webStream = webResponse.GetResponseStream() ?? Stream.Null)
                using (StreamReader responseReader = new StreamReader(webStream))
                {
                    string response = responseReader.ReadToEnd();
                    returnedTokenData obj = JsonConvert.DeserializeObject<returnedTokenData>(response);  

                    return obj.tokenId;
                }
            }
            catch
            {
                HttpWebRequest request2 = (HttpWebRequest)WebRequest.Create(URL);
                request2.Method = "POST";
                request2.Accept = "application/json";
                request2.ContentType = "application/json";
                request2.MediaType = "application/json";
                request2.PreAuthenticate = true;
                request2.AllowAutoRedirect = true;
                request2.Headers.Add("X-OpenAM-Username", "anonymous");
                request2.Headers.Add("X-OpenAM-Password", "anonymous");
                try
                {

                    WebResponse webResponse = request2.GetResponse();
                    using (Stream webStream = webResponse.GetResponseStream() ?? Stream.Null)
                    using (StreamReader responseReader = new StreamReader(webStream))
                    {
                        string response = responseReader.ReadToEnd();
                        returnedTokenData obj = JsonConvert.DeserializeObject<returnedTokenData>(response);
                        return obj.tokenId;
                    }
                }
                catch (Exception e)
                {
                }

            }
            return "Could not get token";
        }

        public class returnedTokenData
        {
            public string tokenId { get; set; }
            public string successUrl { get; set; }
        }
    }
    class Solution_Dproj_Mapping
    {
        #region Functions
        public static string getDprojIdGeneral(string projectName)
        {
            string URL;
            try
            {
                URL = NiceRestHelpers.fetchFQDNfromConfig() + @"/RTServer/rest/nice/rti/core/solution-assignments/solutions?active=true";
            }
            catch
            {
                URL = ConfigurationManager.AppSettings["FQDN"] + @"/RTServer/rest/nice/rti/core/solution-assignments/solutions?active=true";
            }
             HttpWebRequest request = null;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(URL);
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not parse URL string. Please make sure FQDN under AppSettings is configured in the client's root folder");
                throw new Exception("Failed to create request");
            }
            request.Method = "GET";
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.MediaType = "application/json";
            request.Headers.Add("Cookie", "iPlanetDirectoryPro=" + Get_Token.getToken());
            try
            {
                //if (Common_Ground.logArchitect.IsDebugEnabled)
                //Common_Ground.logArchitect.Error("Common Ground- sending URI: " + request.RequestUri.ToString());
                WebResponse webResponse = request.GetResponse();
                using (Stream webStream = webResponse.GetResponseStream() ?? Stream.Null)
                using (StreamReader responseReader = new StreamReader(webStream))
                {
                    string response = responseReader.ReadToEnd();
                    ReturnedSolutionsList returnedSolutionsList = JsonConvert.DeserializeObject<ReturnedSolutionsList>(response);
                    foreach (Solution_Dproj_Mapping.Solution solution in returnedSolutionsList.solutions)
                    {
                        if (solution.name == projectName)
                            return solution.solutionId;
                    }
                    throw new Exception("Solution Not Found - Please verify package is assigned to the user");
                }
            }
            catch (Exception e)
            {
            }
            throw new Exception("Solution Not Found - Please verify package is assigned to the user");
        }

        public static string getDprojId(string projectName)
        {
            string URL = null;
            try
            {
                if (Direct.Shared.Environment_Functions.TeamID == null)
                {
                    throw new Exception("No Team ID identified");
                }
                try
                {
                    URL = NiceRestHelpers.fetchFQDNfromConfig() + @"/RTServer/rest/nice/rti/core/solution-assignments/" +
                    Direct.Shared.Environment_Functions.TeamID.Replace(@"\", "$$$");
                }
                catch
                {
                    URL = ConfigurationManager.AppSettings["FQDN"] + @"/RTServer/rest/nice/rti/core/solution-assignments/" +
                    Direct.Shared.Environment_Functions.TeamID.Replace(@"\", "$$$");
                }

            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            HttpWebRequest request = null;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(URL);
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not parse URL string. Please make sure FQDN under AppSettings is configured in the client's root folder");
                throw new Exception("Failed to create request");
            }
            request.Method = "GET";
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.MediaType = "application/json";
            request.Headers.Add("Cookie", "iPlanetDirectoryPro=" + Get_Token.getToken());
            try
            {
                //if (Common_Ground.logArchitect.IsDebugEnabled)
                //Common_Ground.logArchitect.Error("Common Ground- sending URI: " + request.RequestUri.ToString());
                WebResponse webResponse = request.GetResponse();
                using (Stream webStream = webResponse.GetResponseStream() ?? Stream.Null)
                using (StreamReader responseReader = new StreamReader(webStream))
                {
                    string response = responseReader.ReadToEnd();
                    ReturnedAssignmentData assignedSolution = JsonConvert.DeserializeObject<ReturnedAssignmentData>(response);
                    foreach (SelfSolution selfSolution in assignedSolution.selfSolutions)
                    {
                        if (selfSolution.solution.display == projectName)
                            return selfSolution.solution.solutionId;
                    }
                    throw new Exception("Solution Not Found  - Please verify package is assigned to the user");
                }
            }
            catch (Exception e)
            {
            }
            throw new Exception("Solution Not Found - Please verify package is assigned to the user");
        }

        public static List<SelfSolution> getAssignedVersion()
        {
            string URL = null;
            try
            {
                try
                {
                    URL = NiceRestHelpers.fetchFQDNfromConfig() + @"/RTServer/rest/nice/rti/core/solution-assignments/" +
                    Direct.Shared.Environment_Functions.TeamID.Replace(@"\", "$$$");
                }
                catch
                {
                    URL = ConfigurationManager.AppSettings["FQDN"] + @"/RTServer/rest/nice/rti/core/solution-assignments/" +
                    Direct.Shared.Environment_Functions.TeamID.Replace(@"\", "$$$");
                }

            }
            catch (Exception e)
            {
                return new List<SelfSolution>();
            }
            HttpWebRequest request = null;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(URL);
            }
            catch (Exception e)
            {
                throw new Exception("Failed to create request");
            }
            request.Method = "GET";
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.MediaType = "application/json";
            request.Headers.Add("Cookie", "iPlanetDirectoryPro=" + Get_Token.getToken());
            try
            {
                //if (Common_Ground.logArchitect.IsDebugEnabled)
                //Common_Ground.logArchitect.Error("Common Ground- sending URI: " + request.RequestUri.ToString());
                WebResponse webResponse = request.GetResponse();
                using (Stream webStream = webResponse.GetResponseStream() ?? Stream.Null)
                using (StreamReader responseReader = new StreamReader(webStream))
                {
                    string response = responseReader.ReadToEnd();
                    ReturnedAssignmentData returnedData = JsonConvert.DeserializeObject<ReturnedAssignmentData>(response);
                    return returnedData.selfSolutions;
                }
            }
            catch (Exception e)
            {
            }
            return new List<SelfSolution>();
        }
        #endregion

        public class Version
        {
            public int versionNumber { get; set; }
            public string description { get; set; }
        }

        public class Solution
        {
            public string solutionId { get; set; }
            public string name { get; set; }
            public List<Version> versions { get; set; }
        }

        public class ReturnedSolutionsList
        {
            public List<Solution> solutions { get; set; }
        }
    }

    public class Solution
    {
        public string solutionId { get; set; }
        public int version { get; set; }
        public string display { get; set; }
        public string notes { get; set; }
    }

    public class SelfSolution
    {
        public string teamId { get; set; }
        public bool loadOnStartup { get; set; }
        public object assignedOn { get; set; }
        public string assignedBy { get; set; }
        public Solution solution { get; set; }
    }

    public class ReturnedAssignmentData
    {
        public List<SelfSolution> selfSolutions { get; set; }
        public List<object> parentSolutions { get; set; }
        public bool viewable { get; set; }
        public bool editable { get; set; }
    }    
}
