using Pege.Resource;
using System.ComponentModel.DataAnnotations;

namespace Pege.Data
{
    /// <summary>
    /// Информация о стриме-ретрансляторе.
    /// </summary>
    public abstract class RelayStreamInfo : StreamInfo
    {
        /// <summary>
        /// Адрес ретранслируемого стрима.
        /// </summary>
        [Required(
            ErrorMessageResourceType = typeof(Error),
            ErrorMessageResourceName = nameof(Error.UriIsRequired)
        )]
        public string? Uri { get; set; }
    }
}