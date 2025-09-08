using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace LNAB
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "LNABService" in both code and config file together.
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class PatientService : IService1
    {


        public void reStart()
        {
          Form1.reStart();
        }

        public void startProcess()
        {
            Form1.preStart();
        }
    }
}
