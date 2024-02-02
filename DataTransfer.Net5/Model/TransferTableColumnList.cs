using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msa.Data.Transfer.Model
{
	/// <summary> Eine Liste von Spaltenmappings </summary>
	[Serializable]
	public class TransferTableColumnList : List<TransferTableColumn>
	{
		/// <summary> Fügt ein neues Spaltenmapping hinzu </summary>
		/// <param name="source">Der Name der Spalte in der Quelltabelle</param>
		/// <param name="target">Der Name der Spalte in der Zieltabelle</param>
		public void Add(string source, string target)
		{
			TransferTableColumn col = new TransferTableColumn();
			col.sourceCol = source;
			col.targetCol = target;
			this.Add(col);
		}



        /// <summary> Ermöglicht Indexzugriff auf die Spaltenmappings </summary>
        /// <param name="index">Der 0 basierte Index zum Zugriff auf ein Spaltenmapping</param>
        /// <returns>Ein konkretes Spaltenmapping</returns>
        public TransferTableColumn this[string index]
		{
			get
			{
				return this.FirstOrDefault((el) => el.sourceCol == index);
			}
		}
	}
}
