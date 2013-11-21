using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace ProfilerLib
{
    public static class MethodLibrary
    {
        static Dictionary<int, MethodBase> lib = new Dictionary<int, MethodBase>();
        public static MethodBase GetInfo(int fromAddress)
        {
            MethodBase meth;
            if (lib.TryGetValue(fromAddress, out meth))
                return meth;
            throw new Exception("Could not resolve Method-Address [" + fromAddress.ToString() + "] to method data.");
        }
        public static void GetText(int fromAddress, StringBuilder target)
        {
            // todo: add arguments, generics and sth like that if we don't switch to a 3rd-party-ui (slimtune?)
            var method = GetInfo(fromAddress);
            target
                .Append((method == null || method.DeclaringType == null) ? "UNKNOWN_TYPE" : method.DeclaringType.FullName)
                .Append('.')
                .Append((method == null) ? "UNKNOWN_METHOD" : method.Name);
        }
        public static string GetText(int fromAddress)
        {
            var sb = new StringBuilder();
            GetText(fromAddress, sb);
            return sb.ToString();
        }
        public static void Register(int address, MethodBase method)
        {
            if (lib.ContainsKey(address))
            {
                //Skip dupes... this does atm happen e.g. for generics
                if (lib[address] == method)
                    return;
                throw new Exception("Trying to assging [" + address + "] to " + method.DeclaringType.FullName + "." + method.Name + ", but it is already listed as " + lib[address].DeclaringType.FullName + "." + lib[address].Name);
            }
            lib.Add(address, method);
        }
    }
}
