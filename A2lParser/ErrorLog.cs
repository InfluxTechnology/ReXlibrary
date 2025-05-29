using System;
using System.Collections.Generic;
using System.Text;

namespace A2lParserLib
{
    public enum ErrorType
    {
        Warning,
        Error
    }

    public class Error
    {
        public string Message { get; set; }
        public ErrorType ErrorType { get; set; }
    }

    public class ErrorLog
    {
        
        public List<Error> Log { get; set; } = new List<Error>();
        public void AddError(string ErrorMessage)
        {
            Log.Add(new Error() { ErrorType = ErrorType.Error , Message = ErrorMessage});
        }
        public void AddWarning(string ErrorMessage)
        {
            Log.Add(new Error() { ErrorType = ErrorType.Warning, Message = ErrorMessage });
        }
    }
}
