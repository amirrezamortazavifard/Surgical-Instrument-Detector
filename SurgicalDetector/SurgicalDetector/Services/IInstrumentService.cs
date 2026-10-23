using SurgicalDetector.Models;
using System.Collections.Generic;

namespace SurgicalDetector.Services
{
    public interface IInstrumentService
    {
        IEnumerable<SurgicalInstrument> GetAll();
    }
}