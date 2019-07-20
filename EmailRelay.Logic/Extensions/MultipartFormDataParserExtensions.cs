using HttpMultipartParser;

namespace EmailRelay.Logic.Extensions
{
    public static class MultipartFormDataParserExtensions
    {
        public static string GetParameterValue(this MultipartFormDataParser parser, string parameterName, string defaultValue)
            => parser.HasParameter(parameterName) ? parser.GetParameterValue(parameterName) : defaultValue;
    }
}
