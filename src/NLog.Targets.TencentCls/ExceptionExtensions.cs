using System;

namespace NLog.Targets.TencentClsTarget
{
    internal static class ExceptionExtensions
    {
        public static Exception FlattenToActualException(this Exception exception)
        {
            if (!(exception is AggregateException aggregateException))
                return exception;

            var flattenException = aggregateException.Flatten();
            if (flattenException.InnerExceptions.Count == 1)
            {
                return flattenException.InnerExceptions[0];
            }

            return flattenException;
        }
    }
}