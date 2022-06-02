# InvokeTask Library

Libary that provides the option to invoke task with business data in Automation Studio.

### Invoke Task

##### Description:
Invokes a new robotic task in the RTServer.

#### Usage:
1. Set the OpenAM username and password by using the function **Set OpenAM Credentials**
2. Use Invoke function

#### Expected parameters:
- Invoke Solution ID – the dproj id. (dproj_xxxxxxxxxxxx).
- Workflow ID – the id the of workflow. ($Project.workflowItem_xxxxxxxx).
- Priority – the priority of the task, medium priority is 4.
- Input Data – The robotic’s workflow input as a JSON string.
- Business Data – ten fields of business data, require a “|” between each field. Important note – default size of business data field is 100 characters. (BUSINESSDATA1|BUSINESSDATA2|…|BUSINESSDATA10)

#### Returns:
If invoke failed or succeeded. If succeeded, it will include the Invoke TaskID.

#### Install:
- Open AS Package Generator.
- Select the DLL.
- Name the package.
- Import into a project from inside the AS.

#### Verified Compatibility:
- NICE APA 7.x

*Disclaimer: this is a product of PAteam meant for the NICE community and is not created or supported by NICE*
