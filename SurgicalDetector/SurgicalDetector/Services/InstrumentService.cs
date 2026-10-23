using SurgicalDetector.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SurgicalDetector.Services
{
    public class InstrumentService : IInstrumentService
    {
        private IEnumerable<SurgicalInstrument> _instrumentsCache;
        private readonly string _jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "instruments.json");

        public IEnumerable<SurgicalInstrument> GetAll()
        {
            if (_instrumentsCache == null)
            {
                if (!File.Exists(_jsonFilePath))
                {
                    throw new FileNotFoundException("The instrument data file was not found.", _jsonFilePath);
                }

                var jsonContent = File.ReadAllText(_jsonFilePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                _instrumentsCache = JsonSerializer.Deserialize<List<SurgicalInstrument>>(jsonContent, options);
            }
            return _instrumentsCache;
        }
    }
}