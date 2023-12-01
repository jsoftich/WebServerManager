using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using iControl;

namespace WebServerManager
{
    class f5
    {
        public bool bInitialized;
        private string IPAddress;
        private string User;
        private string Password;
        private int Port;

        public f5()
        {
            IPAddress = ConfigurationManager.AppSettings["f5IP"];
            User = ConfigurationManager.AppSettings["f5Username"];
            Password = ConfigurationManager.AppSettings["f5Password"];
            Port = Int32.TryParse(ConfigurationManager.AppSettings["f5Port"],out Port) ? Port :443;

            this.bInitialized = m_interfaces.initialize(IPAddress, Port, User, Password);
        }

        Interfaces m_interfaces = new Interfaces();
        

        public void listPools()
        {

            String[] pool_list = m_interfaces.LocalLBPool.get_list();
            Console.WriteLine("POOLS");
            for (int i = 0; i < pool_list.Length; i++)
            {
                Console.WriteLine("  [" + i + "] : " + pool_list[i]);
            }
        }

        public void listPoolMembers(String pool_name)
        {
            CommonIPPortDefinition[][] members = m_interfaces.LocalLBPool.get_member(new string[] { pool_name });
            Console.WriteLine("POOL '" + pool_name + "' MEMBERS");
            for (int i = 0; i < members[0].Length; i++)
            {
                Console.WriteLine("  [" + i + "] : " + members[0][i].address + ":" + members[0][i].port.ToString());
            }
        }
        public void showPoolMemberState(String pool_name, String member)
        {
            CommonIPPortDefinition ipPort = parseMember(member);
            LocalLBPoolMemberMemberSessionState[][] session_states =
            m_interfaces.LocalLBPoolMember.get_session_enabled_state(new string[] { pool_name });
            Console.WriteLine("POOL '" + pool_name + "' MEMBER STATUS");
            for (int i = 0; i < session_states[0].Length; i++)
            {
                if (session_states[0][i].member.address.Equals(ipPort.address) &&
                    session_states[0][i].member.port == ipPort.port)
                {
                    Console.WriteLine("  " + member + " : " + session_states[0][i].session_state);
                }
            }
        }

        public string getPoolMemberState(String pool_name, String member)
        {
            CommonIPPortDefinition ipPort = parseMember(member);
            string sReturn = string.Empty;
            LocalLBPoolMemberMemberSessionState[][] session_states =
            m_interfaces.LocalLBPoolMember.get_session_enabled_state(new string[] { pool_name });
            //Console.WriteLine("POOL '" + pool_name + "' MEMBER STATUS");
            for (int i = 0; i < session_states[0].Length; i++)
            {
                if (session_states[0][i].member.address.Equals(ipPort.address) &&
                    session_states[0][i].member.port == ipPort.port)
                {
                    sReturn = session_states[0][i].session_state.ToString();
                }
            }
            return sReturn;
        }

        public void setPoolMemberState(String pool_name, String member, String state)
        {
            CommonIPPortDefinition ipPort = parseMember(member);
            if (null != ipPort)
            {
                LocalLBPoolMemberMemberSessionState[][] session_states = new LocalLBPoolMemberMemberSessionState[1][];
                session_states[0] = new LocalLBPoolMemberMemberSessionState[1];
                session_states[0][0] = new LocalLBPoolMemberMemberSessionState();
                session_states[0][0].member = new CommonIPPortDefinition();
                session_states[0][0].member = ipPort;
                session_states[0][0].session_state = parseState(state);
                m_interfaces.LocalLBPoolMember.set_session_enabled_state(new string[] { pool_name }, session_states);
                Console.WriteLine("Setting state to " + session_states[0][0].session_state);
            }
        }

        public CommonEnabledState parseState(String str)
        {
            CommonEnabledState state = CommonEnabledState.STATE_ENABLED;
            if (str.ToLower().Equals("enable"))
            {
                state = CommonEnabledState.STATE_ENABLED;
            }
            else if (str.ToLower().Equals("disable"))
            {
                state = CommonEnabledState.STATE_DISABLED;
            }
            return state;
        }

        public CommonIPPortDefinition parseMember(String member)
        {
            CommonIPPortDefinition ipPort = null;
            String[] sSplit = member.Split(new char[] { ':' });
            if (2 == sSplit.Length)
            {
                ipPort = new CommonIPPortDefinition();
                ipPort.address = sSplit[0];
                ipPort.port = Convert.ToInt32(sSplit[1]);
            }
            return ipPort;
        }

    }
}
