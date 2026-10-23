using SurgicalDetector.Models;
using SurgicalDetector.Services;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace SurgicalDetector.ViewModels
{
    public class InstrumentDatabaseViewModel : BaseViewModel
    {
        private readonly IInstrumentService _instrumentService;
        private readonly CollectionViewSource _instrumentsViewSource;
        private SurgicalInstrument _selectedInstrument;
        private string _searchText;

        public ICollectionView InstrumentsView => _instrumentsViewSource.View;

        public SurgicalInstrument SelectedInstrument
        {
            get => _selectedInstrument;
            set => SetProperty(ref _selectedInstrument, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    InstrumentsView.Refresh();
                }
            }
        }

        public InstrumentDatabaseViewModel(IInstrumentService instrumentService, IUIService uiService)
        {
            _instrumentService = instrumentService;

            IEnumerable<SurgicalInstrument> instruments = new List<SurgicalInstrument>();
            try
            {
                instruments = _instrumentService.GetAll();
            }
            catch (System.Exception ex)
            {
                uiService.ShowError($"Failed to load instrument data: {ex.Message}");
            }

            _instrumentsViewSource = new CollectionViewSource
            {
                Source = instruments
            };

            InstrumentsView.Filter = FilterInstruments;
            SelectedInstrument = instruments.FirstOrDefault();
        }

        private bool FilterInstruments(object item)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            if (item is SurgicalInstrument instrument)
            {
                var searchTextLower = SearchText.ToLowerInvariant();

                return instrument.Name.ToLowerInvariant().Contains(searchTextLower) ||
                       instrument.Category.ToLowerInvariant().Contains(searchTextLower) ||
                       (instrument.Aliases?.Any(alias => alias.ToLowerInvariant().Contains(searchTextLower)) ?? false);
            }

            return false;
        }
    }
}