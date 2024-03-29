# AWS Lambda Simple S3 Function Project

This starter project consists of:

* Function.cs - class file containing a class with a single function handler method
* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS

You may also have a test project, depending on the options selected.

The generated function handler responds to events on an Amazon S3 bucket. The handler receives the bucket and object key details in an S3Event instance and returns the object's content type as the function output. Replace the body of this method and parameters to suit your needs.

After deploying your function, you must configure an Amazon S3 bucket as an event source to trigger your Lambda function.

## Here are some steps to follow from Visual Studio:

To deploy your function to AWS Lambda, right-click the project in Solution Explorer and select _Publish to AWS Lambda_.

To view your deployed function, open its Function View window by double-clicking the function name shown beneath the AWS Lambda node in the AWS Explorer tree.

To perform testing against your deployed function, use the Test Invoke tab in the opened Function View window.

To configure event sources for your deployed function, for example, to have your function invoked when an object is created in an Amazon S3 bucket, use the Event Sources tab in the opened Function View window.

To update the runtime configuration of your deployed function, use the Configuration tab in the opened Function View window.

To view execution logs of invocations of your function, use the Logs tab in the opened Function View window.

Once you have edited your template and code, you can deploy your application using:

Install Amazon.Lambda.Tools Global Tools if not already installed.

```
    dotnet tool install -g Amazon.Lambda.Tools
```

If already installed, check if a new version is available.

```
    dotnet tool update -g Amazon.Lambda.Tools
```

Deploy function to AWS Lambda

```
    cd "Demo/AWS_Lambda"
    dotnet lambda deploy-function
```

!!! Add S3 Trigger for the Lambda function with .xml, .rxd extension. When converting XML to .rxc, the .xsd schema must be in the same folder as the XML file. When converting RXD to a .csv file, the DBC file must be in the same folder as the RXD file.
