using System.Collections.Generic;

namespace Spark
{
	/// <summary>
	/// Data container interface. Parent for other data containers such as Events, Matches, Goals, Players...
	/// </summary>
	interface DataContainer
	{
		Dictionary<string, object> ToDict();

	}
}
