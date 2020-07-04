using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace xpra
{
    public abstract class BaseConverter : MarkupExtension
    {
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }

    public class BoolToVis : BaseConverter, IValueConverter
    {
        public bool Negate { get; set; }
        public BoolToVis()
        {
            Negate = false;
        }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!Negate)
                return (bool)value ? Visibility.Visible : Visibility.Collapsed;
            else
                return !(bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class PageToVis : BaseConverter, IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return value.ToString() == parameter.ToString() ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
    public class NegateBoolConverter : BaseConverter, IValueConverter
    {
        //public NegateBoolConverter() { }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool original = (bool)value;
            return !original;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool original = (bool)value;
            return !original;
        }
    }


    public class DisplayToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ReadOnlyObservableCollection<Object>)
            {
                var items = (ReadOnlyObservableCollection<Object>)value;
                if (items.Count > 0)
                {
                    return ((Ap)items[0]).DisplayStatus.ToString();
                }
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value;
        }
    }
    public class DisplayToActionTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ReadOnlyObservableCollection<Object>)
            {
                var items = (ReadOnlyObservableCollection<Object>)value;
                if (items.Count > 0)
                {
                    var status = ((Ap)items[0]).DisplayStatus;
                    if (status == DisplayStatus.ACTIVE)
                        return "DETACH";
                    else
                        return "ATTACH";
                }                
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value;
        }
    }
}
