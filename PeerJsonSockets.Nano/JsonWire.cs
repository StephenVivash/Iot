namespace PeerJsonSockets.Nano
{
	internal static class JsonWire
	{
		public static string GetString(string json, string propertyName)
		{
			string marker = "\"" + propertyName + "\"";
			int propertyIndex = json.IndexOf(marker);
			if (propertyIndex < 0)
			{
				return string.Empty;
			}

			int colonIndex = json.IndexOf(':', propertyIndex + marker.Length);
			if (colonIndex < 0)
			{
				return string.Empty;
			}

			int start = json.IndexOf('"', colonIndex + 1);
			if (start < 0)
			{
				return string.Empty;
			}

			int end = start + 1;
			while (end < json.Length)
			{
				if (json[end] == '"' && json[end - 1] != '\\')
				{
					break;
				}

				end++;
			}

			if (end >= json.Length)
			{
				return string.Empty;
			}

			return Replace(Replace(json.Substring(start + 1, end - start - 1), "\\\"", "\""), "\\\\", "\\");
		}

		public static string GetObject(string json, string propertyName)
		{
			string marker = "\"" + propertyName + "\"";
			int propertyIndex = json.IndexOf(marker);
			if (propertyIndex < 0)
			{
				return "{}";
			}

			int colonIndex = json.IndexOf(':', propertyIndex + marker.Length);
			if (colonIndex < 0)
			{
				return "{}";
			}

			int start = colonIndex + 1;
			while (start < json.Length && (json[start] == ' ' || json[start] == '\t'))
			{
				start++;
			}

			if (start >= json.Length)
			{
				return "{}";
			}

			if (json[start] == '{')
			{
				return ReadBalanced(json, start, '{', '}');
			}

			if (json[start] == '[')
			{
				return ReadBalanced(json, start, '[', ']');
			}

			int end = json.IndexOf(',', start);
			if (end < 0)
			{
				end = json.IndexOf('}', start);
			}

			if (end < 0)
			{
				end = json.Length;
			}

			return json.Substring(start, end - start);
		}

		private static string ReadBalanced(string json, int start, char open, char close)
		{
			bool inString = false;
			int depth = 0;

			for (int i = start; i < json.Length; i++)
			{
				char c = json[i];
				if (c == '"' && (i == start || json[i - 1] != '\\'))
				{
					inString = !inString;
				}

				if (inString)
				{
					continue;
				}

				if (c == open)
				{
					depth++;
				}
				else if (c == close)
				{
					depth--;
					if (depth == 0)
					{
						return json.Substring(start, i - start + 1);
					}
				}
			}

			return "{}";
		}

		private static string Replace(string value, string oldValue, string newValue)
		{
			int index = value.IndexOf(oldValue);
			if (index < 0)
			{
				return value;
			}

			string result = string.Empty;
			int start = 0;
			while (index >= 0)
			{
				result += value.Substring(start, index - start);
				result += newValue;
				start = index + oldValue.Length;
				index = value.IndexOf(oldValue, start);
			}

			result += value.Substring(start);
			return result;
		}
	}
}
