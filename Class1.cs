using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using log4net;
using System.IO;
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
        internal static string _openAMUser;
        internal static string _openAMPassword;

        [DirectDom("Set OpenAM Credentials")]
        [DirectDomMethod("Set OpenAM Credentials {username} {password}")]
        [MethodDescriptionAttribute("Sets OpenAM credentials necessary to authenticate to invoke a task")]
        public static void setOpenAMCredentials(string username, string password)
        {
            _openAMUser = username;
            _openAMPassword = password;

        }

        [DirectDom("Invoke")]
        [DirectDomMethod("Invoke Solution ID {solutionId}, Workflow ID {workflowId}, Priority {priority}, Input Data {inputData}, Business Data {businessData}")]
        [MethodDescriptionAttribute("Creates a task with the information given in the type")]
        public static string invokeTask(string solutionID, string wfID, int priority, string data, string businessData)
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
                return InnerInvokeTask(solutionID, wfID, priority, ConvertStringToList(data), ConvertStringToList(businessData));

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
            List<string> parsedDataList = new List<string>();

            if (string.IsNullOrEmpty(text))
            {
                return parsedDataList;
            }

            parsedDataList = text.Split('|').ToList();
            return parsedDataList;
        }
        private static string InnerInvokeTask(string solutionID, string wfID, int priority, List<string> data, List<string> businessData)
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

            foreach (string input in data)
            {
                workflowData.arguments.Add(new Argument("String", input));
            }

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
                if (logArchitect.IsDebugEnabled)
                {
                    logArchitect.Info("Trying to create a task for SolutionID: \"" + solutionID + "\" on workflow: \"" + wfID +
                        "\" with input: " + data);
                    logArchitect.Info("Request data was:\n" + JsonConvert.SerializeObject(bodyRoot, Formatting.Indented).Replace("\"{", "{").Replace("}\"", "}").Replace(@"\", ""));
                }
                WebResponse webResponse = request.GetResponse();
                using (Stream webStream = webResponse.GetResponseStream() ?? Stream.Null)
                using (StreamReader responseReader = new StreamReader(webStream))
                {
                    string response = responseReader.ReadToEnd();
                    AnswerRoot answer = JsonConvert.DeserializeObject<AnswerRoot>(response);
                    if (logArchitect.IsDebugEnabled)
                        logArchitect.Debug("InvokeTask task ID: " + answer.requestId);
                    //return answer.message;
                    return "Success " + answer.requestId;
                }
            }
            catch (Exception e)
            {
                if (logArchitect.IsErrorEnabled)
                    logArchitect.Error("Failed to create a task for SolutionID:\n" + solutionID + "\" on workflow:\n" + workflowMetaData.workflowId +
                        "\" with input:\n" + data + ".\nReason: " + e.Message);
            }
            return "Failed to invoke, check logs for more information";
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
            request.Headers.Add("X-OpenAM-Username", TaskInvoker._openAMUser);
            request.Headers.Add("X-OpenAM-Password", TaskInvoker._openAMPassword);

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
}
