using System;
using System.Collections.Generic;
using System.Text;

namespace Emby.AutoOrganize.Core
{
    /// <summary>
    /// An generic exception that occurs during file organization.
    /// </summary>
    public class OrganizationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OrganizationException"/> class.
        /// </summary>
        public OrganizationException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrganizationException"/> class with a specified error message.
        /// </summary>
        /// <param name="msg">The message that describes the error.</param>
        public OrganizationException(string msg) : base(msg)
        {
        }
    }
}
