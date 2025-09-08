using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace LNAB
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "ILNABService" in both code and config file together.
    [ServiceContract]
    public interface IService1
    {
        [OperationContract]
        void reStart();

        [OperationContract]
        void startProcess();
    }
}
