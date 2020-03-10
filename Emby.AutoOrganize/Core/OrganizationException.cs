using System;
using System.Collections.Generic;
using System.Text;

namespace Emby.AutoOrganize.Core
{
    public class OrganizationException : Exception
    {
        public OrganizationException()
        {
        }

        public OrganizationException(string msg) : base(msg)
        {
        }
    }
}
