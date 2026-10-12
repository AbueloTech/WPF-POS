Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows.Controls.Primitives
Imports System.Globalization
Imports System.IO
Imports System.Windows.Documents
Imports System.Windows.Media.Imaging
Imports System.Printing
Imports System.Threading.Tasks
Imports LiveCharts
Imports LiveCharts.Wpf

Namespace Global.NewPOS
Partial Public Class MainWindow
    Inherits Window
    Private _sidebarExpanded As Boolean = True
    Private _principalExpanded As Boolean = True
    Private _ventasExpanded As Boolean = True

    Public Sub New()
        System.Windows.Application.LoadComponent(Me, New Uri("/NewPOS;component/mainwindow.xaml", UriKind.Relative))
    End Sub

    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs) Handles MyBase.Loaded
        WireMenuButtons(Me)
        NavigateTo("Inicio")
    End Sub

    Private Sub WireMenuButtons(parent As DependencyObject)
        For Each button As Button In FindVisualChildren(Of Button)(parent)
            If button.Tag Is Nothing Then Continue For
            Dim destination = button.Tag.ToString()
            Select Case destination
                Case "@sidebar"
                    AddHandler button.Click, Sub(s, args) ToggleSidebar_Click(s, args)
                Case "@principal"
                    AddHandler button.Click, Sub(s, args) TogglePrincipal_Click(s, args)
                Case "@ventas"
                    AddHandler button.Click, Sub(s, args) ToggleVentas_Click(s, args)
                Case Else
                    AddHandler button.Click, AddressOf Navigate_Click
            End Select
        Next
    End Sub

    Public Sub ToggleSidebar_Click(sender As Object, e As RoutedEventArgs)
        _sidebarExpanded = Not _sidebarExpanded
        Named(Of ColumnDefinition)("SidebarColumn").Width = New GridLength(If(_sidebarExpanded, 248, 72))
        Named(Of FrameworkElement)("BrandText").Visibility = If(_sidebarExpanded, Visibility.Visible, Visibility.Collapsed)
        Named(Of FrameworkElement)("CollapseText").Visibility = If(_sidebarExpanded, Visibility.Visible, Visibility.Collapsed)
        Named(Of FrameworkElement)("ExitText").Visibility = If(_sidebarExpanded, Visibility.Visible, Visibility.Collapsed)
        Named(Of FrameworkElement)("WorkspaceLabel").Visibility = If(_sidebarExpanded, Visibility.Visible, Visibility.Collapsed)
        Named(Of FrameworkElement)("OperationLabel").Visibility = If(_sidebarExpanded, Visibility.Visible, Visibility.Collapsed)
        Named(Of FrameworkElement)("AdminLabel").Visibility = If(_sidebarExpanded, Visibility.Visible, Visibility.Collapsed)
        Named(Of FrameworkElement)("ProfilePanel").Visibility = If(_sidebarExpanded, Visibility.Visible, Visibility.Collapsed)
        For Each button As Button In FindVisualChildren(Of Button)(Me)
            If TypeOf button.Parent Is StackPanel AndAlso (button.Tag IsNot Nothing OrElse button.ToolTip IsNot Nothing) Then
                button.Padding = New Thickness(If(_sidebarExpanded, 12, 0), 0, 12, 0)
                If Not _sidebarExpanded Then
                    Dim panel = TryCast(button.Content, StackPanel)
                    If panel IsNot Nothing AndAlso panel.Children.Count > 1 Then
                        For i = 1 To panel.Children.Count - 1
                            panel.Children(i).Visibility = Visibility.Collapsed
                        Next
                    End If
                Else
                    RestoreSidebarLabels()
                End If
            End If
        Next
        Named(Of FrameworkElement)("PrincipalSubmenu").Visibility = If(_sidebarExpanded AndAlso _principalExpanded, Visibility.Visible, Visibility.Collapsed)
        Named(Of FrameworkElement)("VentasSubmenu").Visibility = If(_sidebarExpanded AndAlso _ventasExpanded, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Sub RestoreSidebarLabels()
        For Each textBlock In FindVisualChildren(Of TextBlock)(Me)
            If textBlock.Text <> "" AndAlso (textBlock.FontSize = 12 OrElse textBlock.FontSize = 13) Then textBlock.Visibility = Visibility.Visible
        Next
    End Sub

    Public Sub TogglePrincipal_Click(sender As Object, e As RoutedEventArgs)
        _principalExpanded = Not _principalExpanded
        Named(Of FrameworkElement)("PrincipalSubmenu").Visibility = If(_sidebarExpanded AndAlso _principalExpanded, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Public Sub ToggleVentas_Click(sender As Object, e As RoutedEventArgs)
        _ventasExpanded = Not _ventasExpanded
        Named(Of FrameworkElement)("VentasSubmenu").Visibility = If(_sidebarExpanded AndAlso _ventasExpanded, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Public Sub Navigate_Click(sender As Object, e As RoutedEventArgs)
        Dim button = TryCast(sender, Button)
        If button IsNot Nothing AndAlso button.Tag IsNot Nothing Then NavigateTo(button.Tag.ToString())
    End Sub

    Private Sub NavigateTo(pageName As String)
        If String.Equals(pageName, "Productos", StringComparison.OrdinalIgnoreCase) Then
            Named(Of Frame)("ContentFrame").Navigate(New Global.ProductosPage())
        Else
            Named(Of Frame)("ContentFrame").Navigate(New Global.PosPage(pageName))
        End If
    End Sub

    Private Function Named(Of T As Class)(name As String) As T
        Return DirectCast(FindName(name), T)
    End Function

    Private Iterator Function FindVisualChildren(Of T As DependencyObject)(parent As DependencyObject) As IEnumerable(Of T)
        If parent Is Nothing Then Return
        For i As Integer = 0 To VisualTreeHelper.GetChildrenCount(parent) - 1
            Dim child = VisualTreeHelper.GetChild(parent, i)
            If TypeOf child Is T Then Yield DirectCast(child, T)
            For Each descendant As T In FindVisualChildren(Of T)(child)
                Yield descendant
            Next
        Next
    End Function
End Class
End Namespace
Public Class PosPage
    Inherits Page
    Private _posDatabase As SqliteDatabase
    Private _catalogGrid As UniformGrid
    Private _posCategories As StackPanel
    Private _cartPanel As StackPanel
    Private _customerInput As TextBox
    Private _totalText As TextBlock
    Private _posStatus As TextBlock
    Private _posProducts As New List(Of PlatoRecord)()
    Private _cart As New List(Of PosCartItem)()
    Private _selectedCategory As String = "Todas"
    Private _dashboardDatabase As SqliteDatabase
    Private _dashboardDatePicker As DatePicker
    Private _dashboardWeekLabel As TextBlock
    Private _dashboardStatus As TextBlock
    Private _dashboardKpis As UniformGrid
    Private _salesAreaChart As CartesianChart
    Private _paymentPieChart As PieChart
    Private _categoryPieChart As PieChart
    Private _productBarChart As CartesianChart
    Private _goalProgress As ProgressBar
    Private _goalValueText As TextBlock
    Private _dashboardSales As New List(Of DashboardSale)()
    Private _dashboardDetails As New List(Of DashboardDetail)()
    Private _dashboardRefreshVersion As Integer
    Private _salesDatabase As SqliteDatabase
    Private _salesStartPicker As DatePicker
    Private _salesEndPicker As DatePicker
    Private _salesRows As StackPanel
    Private _salesStatus As TextBlock
    Private _salesSummaryMode As String

    Public Sub New(pageName As String)
        Background = New SolidColorBrush(Color.FromRgb(&HF3, &HF6, &HFA))
        If String.Equals(pageName, "POS", StringComparison.OrdinalIgnoreCase) Then
            _posDatabase = New SqliteDatabase(LocatePosFile("TerrenoBroaster.db"))
            _catalogGrid = New UniformGrid With {.Columns = 4}
            _posCategories = New StackPanel()
            _cartPanel = New StackPanel()
            _customerInput = New TextBox With {.Height = 38, .FontSize = 13, .Padding = New Thickness(10, 8, 10, 8), .BorderBrush = Brush("#D8E1EC"), .BorderThickness = New Thickness(1), .Background = Brush("#F9FBFD")}
            _totalText = New TextBlock With {.Text = "0", .Foreground = Brush("#172B4D"), .FontSize = 29, .FontWeight = FontWeights.Bold}
            _posStatus = New TextBlock With {.Text = "Agrega productos para iniciar una venta.", .Foreground = Brush("#718096"), .FontSize = 10, .TextWrapping = TextWrapping.Wrap}
            Content = BuildPosContent()
            LoadPosProducts()
            RefreshCart()
        ElseIf String.Equals(pageName, "Dashboard", StringComparison.OrdinalIgnoreCase) Then
            _dashboardDatabase = New SqliteDatabase(LocatePosFile("TerrenoBroaster.db"))
            Content = DashboardLoadingView()
            AddHandler Me.Loaded, AddressOf Dashboard_Loaded
        ElseIf String.Equals(pageName, "Ventas por fecha", StringComparison.OrdinalIgnoreCase) OrElse String.Equals(pageName, "xFecha", StringComparison.OrdinalIgnoreCase) Then
            _salesDatabase = New SqliteDatabase(LocatePosFile("TerrenoBroaster.db"))
            _salesStartPicker = New DatePicker With {.SelectedDate = Date.Today, .Width = 150, .Margin = New Thickness(0, 0, 10, 0)}
            _salesEndPicker = New DatePicker With {.SelectedDate = Date.Today, .Width = 150, .Margin = New Thickness(0, 0, 12, 0)}
            _salesRows = New StackPanel()
            _salesStatus = New TextBlock With {.Text = "Selecciona un intervalo para consultar las ventas.", .Foreground = Brush("#718096"), .FontSize = 11, .Margin = New Thickness(2, 8, 2, 0)}
            Content = BuildSalesByDateView()
            AddHandler Me.Loaded, AddressOf SalesPage_Loaded
        ElseIf String.Equals(pageName, "Ventas por producto", StringComparison.OrdinalIgnoreCase) OrElse String.Equals(pageName, "xProducto", StringComparison.OrdinalIgnoreCase) Then
            InitializeSalesSummary("producto")
        ElseIf String.Equals(pageName, "Ventas por forma de pago", StringComparison.OrdinalIgnoreCase) OrElse String.Equals(pageName, "xFormaPago", StringComparison.OrdinalIgnoreCase) Then
            InitializeSalesSummary("pago")
        Else
            Content = If(pageName = "Inicio", BuildHome(), BuildDestination(pageName))
        End If
    End Sub

    Private Function BuildSalesByDateView() As UIElement
        Dim root As New Grid With {.Margin = New Thickness(22)}
        root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        root.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})
        Dim content As New StackPanel()
        content.Children.Add(New TextBlock With {.Text = "VENTAS", .Foreground = Brush("#3478D4"), .FontSize = 10, .FontWeight = FontWeights.Bold})
        content.Children.Add(New TextBlock With {.Text = "Ventas por fecha", .Foreground = Brush("#172B4D"), .FontSize = 25, .FontWeight = FontWeights.SemiBold, .Margin = New Thickness(0, 2, 0, 4)})
        content.Children.Add(New TextBlock With {.Text = "Consulta las ventas y revisa los productos de cada ticket.", .Foreground = Brush("#718096"), .FontSize = 12, .Margin = New Thickness(0, 0, 0, 16)})
        Dim filters As New WrapPanel With {.VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 0, 12)}
        filters.Children.Add(New TextBlock With {.Text = "Fecha inicial", .Foreground = Brush("#52627A"), .FontSize = 11, .FontWeight = FontWeights.SemiBold, .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 7, 0)})
        filters.Children.Add(_salesStartPicker)
        filters.Children.Add(New TextBlock With {.Text = "Fecha final", .Foreground = Brush("#52627A"), .FontSize = 11, .FontWeight = FontWeights.SemiBold, .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(4, 0, 7, 0)})
        filters.Children.Add(_salesEndPicker)
        Dim apply = MakePosButton("Filtrar", "#3478D4", Brushes.White)
        apply.Padding = New Thickness(18, 8, 18, 8)
        AddHandler apply.Click, AddressOf FilterSales_Click
        filters.Children.Add(apply)
        content.Children.Add(filters)
        content.Children.Add(_salesStatus)
        Grid.SetRow(content, 0)
        root.Children.Add(content)
        Dim card As New Border With {.Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .CornerRadius = New CornerRadius(12), .Padding = New Thickness(12), .Margin = New Thickness(0, 158, 0, 0)}
        Dim scroll As New ScrollViewer With {.VerticalScrollBarVisibility = ScrollBarVisibility.Auto, .HorizontalScrollBarVisibility = ScrollBarVisibility.Auto}
        scroll.Content = _salesRows
        card.Child = scroll
        Grid.SetRow(card, 1)
        root.Children.Add(card)
        Return root
    End Function

    Private Sub SalesPage_Loaded(sender As Object, e As RoutedEventArgs)
        LoadSalesByDate()
    End Sub

    Private Sub FilterSales_Click(sender As Object, e As RoutedEventArgs)
        If Not _salesStartPicker.SelectedDate.HasValue OrElse Not _salesEndPicker.SelectedDate.HasValue Then
            MessageBox.Show("Selecciona la fecha inicial y la fecha final.", "Ventas por fecha", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If
        If _salesStartPicker.SelectedDate.Value.Date > _salesEndPicker.SelectedDate.Value.Date Then
            MessageBox.Show("La fecha inicial no puede ser posterior a la fecha final.", "Ventas por fecha", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If
        LoadSalesByDate()
    End Sub

    Private Sub LoadSalesByDate()
        Try
            Dim startDate = _salesStartPicker.SelectedDate.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            Dim endDate = _salesEndPicker.SelectedDate.Value.Date.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            Dim rows = _salesDatabase.GetRows("SELECT Fecha, NTicket, Cliente, Monto, FormaPago FROM Ventas WHERE Fecha >= ? AND Fecha < ? ORDER BY Fecha DESC, NTicket DESC", startDate, endDate)
            _salesRows.Children.Clear()
            AddSalesHeader()
            For Each values In rows
                AddSalesRow(values)
            Next
            If rows.Count = 0 Then _salesRows.Children.Add(New TextBlock With {.Text = "No hay ventas en el intervalo seleccionado.", .Foreground = Brush("#718096"), .Padding = New Thickness(12)})
            _salesStatus.Text = rows.Count.ToString() & If(rows.Count = 1, " venta encontrada.", " ventas encontradas.")
        Catch ex As Exception
            _salesStatus.Text = "No fue posible cargar las ventas: " & ex.Message
            MessageBox.Show("No se pudieron cargar las ventas." & Environment.NewLine & ex.Message, "NewPOS · Ventas", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Sub AddSalesHeader()
        Dim header As New Grid With {.Background = Brush("#F4F7FB"), .Margin = New Thickness(0, 0, 0, 4)}
        For Each width As GridLength In New GridLength() {New GridLength(1.5, GridUnitType.Star), New GridLength(0.8, GridUnitType.Star), New GridLength(1.5, GridUnitType.Star), New GridLength(1, GridUnitType.Star), New GridLength(1.2, GridUnitType.Star), New GridLength(100)}
            header.ColumnDefinitions.Add(New ColumnDefinition With {.Width = width})
        Next
        Dim labels = {"FECHA", "TICKET", "CLIENTE", "MONTO", "FORMA DE PAGO", ""}
        For i = 0 To labels.Length - 1
            Dim label As New TextBlock With {.Text = labels(i), .Foreground = Brush("#718096"), .FontSize = 10, .FontWeight = FontWeights.Bold, .Padding = New Thickness(8, 11, 8, 11), .VerticalAlignment = VerticalAlignment.Center}
            Grid.SetColumn(label, i)
            header.Children.Add(label)
        Next
        _salesRows.Children.Add(header)
    End Sub

    Private Sub AddSalesRow(values As String())
        Dim row As New Grid With {.MinHeight = 48}
        For Each width As GridLength In New GridLength() {New GridLength(1.5, GridUnitType.Star), New GridLength(0.8, GridUnitType.Star), New GridLength(1.5, GridUnitType.Star), New GridLength(1, GridUnitType.Star), New GridLength(1.2, GridUnitType.Star), New GridLength(100)}
            row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = width})
        Next
        Dim dateValue As DateTime
        Dim displayDate = If(DateTime.TryParse(values(0), CultureInfo.InvariantCulture, DateTimeStyles.None, dateValue), dateValue.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("es-CO")), values(0))
        Dim amount As Double
        Double.TryParse(values(3), NumberStyles.Any, CultureInfo.InvariantCulture, amount)
        Dim cells = {displayDate, values(1), values(2), amount.ToString("C0", CultureInfo.GetCultureInfo("es-CO")), values(4)}
        For i = 0 To cells.Length - 1
            Dim label As New TextBlock With {.Text = cells(i), .Foreground = Brush("#34445D"), .FontSize = 11, .Padding = New Thickness(8, 13, 8, 13), .VerticalAlignment = VerticalAlignment.Center, .TextTrimming = TextTrimming.CharacterEllipsis}
            If i = 3 Then label.FontWeight = FontWeights.SemiBold
            Grid.SetColumn(label, i)
            row.Children.Add(label)
        Next
        Dim details = MakePosButton("Detalles", "#EAF2FC", Brush("#2F66A5"))
        details.Padding = New Thickness(10, 6, 10, 6)
        details.Tag = values(1)
        AddHandler details.Click, AddressOf ShowSaleDetails_Click
        Grid.SetColumn(details, 5)
        row.Children.Add(details)
        _salesRows.Children.Add(New Border With {.BorderBrush = Brush("#E8EDF3"), .BorderThickness = New Thickness(0, 0, 0, 1), .Child = row})
    End Sub

    Private Sub ShowSaleDetails_Click(sender As Object, e As RoutedEventArgs)
        Dim ticket = Convert.ToString(DirectCast(sender, Button).Tag, CultureInfo.InvariantCulture)
        Try
            Dim rows = _salesDatabase.GetRows("SELECT Plato, Precio, Cantidad, Categoria, Importe FROM Detalles WHERE NTicket = ? ORDER BY rowid", ticket)
            Dim dialog As New Window With {.Title = "Detalles del ticket " & ticket, .Width = 760, .Height = 480, .MinWidth = 600, .MinHeight = 350, .WindowStartupLocation = WindowStartupLocation.CenterOwner, .Owner = Window.GetWindow(Me), .ResizeMode = ResizeMode.CanResize, .Background = Brush("#F3F6FA"), .FontFamily = New FontFamily("Segoe UI")}
            Dim layout As New DockPanel With {.Margin = New Thickness(18)}
            Dim title As New TextBlock With {.Text = "DETALLES DE LA VENTA  ·  TICKET " & ticket, .Foreground = Brush("#172B4D"), .FontSize = 16, .FontWeight = FontWeights.SemiBold, .Margin = New Thickness(0, 0, 0, 14)}
            DockPanel.SetDock(title, Dock.Top)
            layout.Children.Add(title)
            Dim grid As New DataGrid With {.AutoGenerateColumns = False, .IsReadOnly = True, .CanUserAddRows = False, .HeadersVisibility = DataGridHeadersVisibility.Column, .GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, .Background = Brushes.White, .ItemsSource = rows.Select(Function(v) New SaleDetailRow With {.Plato = v(0), .Precio = CurrencyText(v(1)), .Cantidad = v(2), .Categoria = v(3), .Importe = CurrencyText(v(4))}).ToList()}
            grid.Columns.Add(New DataGridTextColumn With {.Header = "Producto", .Binding = New Data.Binding("Plato"), .Width = New DataGridLength(2, DataGridLengthUnitType.Star)})
            grid.Columns.Add(New DataGridTextColumn With {.Header = "Precio", .Binding = New Data.Binding("Precio"), .Width = New DataGridLength(1, DataGridLengthUnitType.Star)})
            grid.Columns.Add(New DataGridTextColumn With {.Header = "Cantidad", .Binding = New Data.Binding("Cantidad"), .Width = New DataGridLength(0.8, DataGridLengthUnitType.Star)})
            grid.Columns.Add(New DataGridTextColumn With {.Header = "Categoría", .Binding = New Data.Binding("Categoria"), .Width = New DataGridLength(1, DataGridLengthUnitType.Star)})
            grid.Columns.Add(New DataGridTextColumn With {.Header = "Importe", .Binding = New Data.Binding("Importe"), .Width = New DataGridLength(1, DataGridLengthUnitType.Star)})
            layout.Children.Add(grid)
            If rows.Count = 0 Then layout.Children.Add(New TextBlock With {.Text = "Este ticket no tiene líneas de detalle.", .Foreground = Brush("#718096"), .Margin = New Thickness(0, 14, 0, 0)})
            dialog.Content = layout
            dialog.ShowDialog()
        Catch ex As Exception
            MessageBox.Show("No se pudieron cargar los detalles de la venta." & Environment.NewLine & ex.Message, "NewPOS · Ventas", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Shared Function CurrencyText(value As String) As String
        Dim amount As Double
        Double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, amount)
        Return amount.ToString("C0", CultureInfo.GetCultureInfo("es-CO"))
    End Function

    Private Class SaleDetailRow
        Public Property Plato As String
        Public Property Precio As String
        Public Property Cantidad As String
        Public Property Categoria As String
        Public Property Importe As String
    End Class

    Private Sub InitializeSalesSummary(mode As String)
        _salesDatabase = New SqliteDatabase(LocatePosFile("TerrenoBroaster.db"))
        _salesSummaryMode = mode
        _salesStartPicker = New DatePicker With {.SelectedDate = Date.Today, .Width = 150, .Margin = New Thickness(0, 0, 10, 0)}
        _salesEndPicker = New DatePicker With {.SelectedDate = Date.Today, .Width = 150, .Margin = New Thickness(0, 0, 12, 0)}
        _salesRows = New StackPanel()
        _salesStatus = New TextBlock With {.Text = "Selecciona un intervalo para consultar los acumulados.", .Foreground = Brush("#718096"), .FontSize = 11, .Margin = New Thickness(2, 8, 2, 0)}
        Content = BuildSalesSummaryView()
        AddHandler Me.Loaded, AddressOf SalesSummary_Loaded
    End Sub

    Private Function BuildSalesSummaryView() As UIElement
        Dim isProduct = _salesSummaryMode = "producto"
        Dim root As New Grid With {.Margin = New Thickness(22)}
        root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        root.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})
        Dim content As New StackPanel()
        content.Children.Add(New TextBlock With {.Text = "VENTAS", .Foreground = Brush("#3478D4"), .FontSize = 10, .FontWeight = FontWeights.Bold})
        content.Children.Add(New TextBlock With {.Text = If(isProduct, "Ventas por producto", "Ventas por forma de pago"), .Foreground = Brush("#172B4D"), .FontSize = 25, .FontWeight = FontWeights.SemiBold, .Margin = New Thickness(0, 2, 0, 4)})
        content.Children.Add(New TextBlock With {.Text = If(isProduct, "Unidades e importe acumulados por producto en el periodo seleccionado.", "Importe acumulado por forma de pago en el periodo seleccionado."), .Foreground = Brush("#718096"), .FontSize = 12, .Margin = New Thickness(0, 0, 0, 16)})
        Dim filters As New WrapPanel With {.VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 0, 12)}
        filters.Children.Add(New TextBlock With {.Text = "Fecha inicial", .Foreground = Brush("#52627A"), .FontSize = 11, .FontWeight = FontWeights.SemiBold, .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 7, 0)})
        filters.Children.Add(_salesStartPicker)
        filters.Children.Add(New TextBlock With {.Text = "Fecha final", .Foreground = Brush("#52627A"), .FontSize = 11, .FontWeight = FontWeights.SemiBold, .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(4, 0, 7, 0)})
        filters.Children.Add(_salesEndPicker)
        Dim apply = MakePosButton("Filtrar", "#3478D4", Brushes.White)
        apply.Padding = New Thickness(18, 8, 18, 8)
        AddHandler apply.Click, AddressOf FilterSalesSummary_Click
        filters.Children.Add(apply)
        content.Children.Add(filters)
        content.Children.Add(_salesStatus)
        Grid.SetRow(content, 0)
        root.Children.Add(content)
        Dim card As New Border With {.Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .CornerRadius = New CornerRadius(12), .Padding = New Thickness(12), .Margin = New Thickness(0, 158, 0, 0)}
        card.Child = New ScrollViewer With {.VerticalScrollBarVisibility = ScrollBarVisibility.Auto, .HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, .Content = _salesRows}
        Grid.SetRow(card, 1)
        root.Children.Add(card)
        Return root
    End Function

    Private Sub SalesSummary_Loaded(sender As Object, e As RoutedEventArgs)
        LoadSalesSummary()
    End Sub

    Private Sub FilterSalesSummary_Click(sender As Object, e As RoutedEventArgs)
        If Not _salesStartPicker.SelectedDate.HasValue OrElse Not _salesEndPicker.SelectedDate.HasValue Then
            MessageBox.Show("Selecciona la fecha inicial y la fecha final.", "Acumulados de ventas", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If
        If _salesStartPicker.SelectedDate.Value.Date > _salesEndPicker.SelectedDate.Value.Date Then
            MessageBox.Show("La fecha inicial no puede ser posterior a la fecha final.", "Acumulados de ventas", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If
        LoadSalesSummary()
    End Sub

    Private Sub LoadSalesSummary()
        Try
            Dim startDate = _salesStartPicker.SelectedDate.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            Dim endDate = _salesEndPicker.SelectedDate.Value.Date.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            Dim rows As List(Of String())
            If _salesSummaryMode = "producto" Then
                rows = _salesDatabase.GetRows("SELECT COALESCE(NULLIF(Plato, ''), 'Sin especificar'), SUM(CAST(Cantidad AS REAL)), SUM(CAST(Importe AS REAL)) FROM Detalles WHERE Fecha >= ? AND Fecha < ? GROUP BY Plato ORDER BY SUM(CAST(Importe AS REAL)) DESC, Plato COLLATE NOCASE", startDate, endDate)
                _salesRows.Children.Clear()
                AddSummaryHeader({"PRODUCTO", "CANTIDAD TOTAL", "IMPORTE TOTAL"}, {New GridLength(2, GridUnitType.Star), New GridLength(1, GridUnitType.Star), New GridLength(1, GridUnitType.Star)})
                For Each values In rows
                    Dim quantity As Double
                    Double.TryParse(values(1), NumberStyles.Any, CultureInfo.InvariantCulture, quantity)
                    AddSummaryRow({values(0), quantity.ToString("0.##", CultureInfo.GetCultureInfo("es-CO")), CurrencyText(values(2))}, {New GridLength(2, GridUnitType.Star), New GridLength(1, GridUnitType.Star), New GridLength(1, GridUnitType.Star)})
                Next
            Else
                rows = _salesDatabase.GetRows("SELECT COALESCE(NULLIF(FormaPago, ''), 'Sin especificar'), COUNT(*), SUM(CAST(Monto AS REAL)) FROM Ventas WHERE Fecha >= ? AND Fecha < ? GROUP BY FormaPago ORDER BY SUM(CAST(Monto AS REAL)) DESC, FormaPago COLLATE NOCASE", startDate, endDate)
                _salesRows.Children.Clear()
                AddSummaryHeader({"FORMA DE PAGO", "VENTAS", "IMPORTE TOTAL"}, {New GridLength(2, GridUnitType.Star), New GridLength(1, GridUnitType.Star), New GridLength(1, GridUnitType.Star)})
                For Each values In rows
                    AddSummaryRow({values(0), values(1), CurrencyText(values(2))}, {New GridLength(2, GridUnitType.Star), New GridLength(1, GridUnitType.Star), New GridLength(1, GridUnitType.Star)})
                Next
            End If
            If rows.Count = 0 Then _salesRows.Children.Add(New TextBlock With {.Text = "No hay ventas en el intervalo seleccionado.", .Foreground = Brush("#718096"), .Padding = New Thickness(12)})
            _salesStatus.Text = rows.Count.ToString() & If(rows.Count = 1, " grupo encontrado.", " grupos encontrados.")
        Catch ex As Exception
            _salesStatus.Text = "No fue posible cargar los acumulados: " & ex.Message
            MessageBox.Show("No se pudieron cargar los acumulados de ventas." & Environment.NewLine & ex.Message, "NewPOS · Ventas", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Sub AddSummaryHeader(labels As String(), widths As GridLength())
        Dim row As New Grid With {.Background = Brush("#F4F7FB"), .Margin = New Thickness(0, 0, 0, 4)}
        For Each width As GridLength In widths
            row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = width})
        Next
        For i = 0 To labels.Length - 1
            Dim label As New TextBlock With {.Text = labels(i), .Foreground = Brush("#718096"), .FontSize = 10, .FontWeight = FontWeights.Bold, .Padding = New Thickness(9, 11, 9, 11), .VerticalAlignment = VerticalAlignment.Center}
            Grid.SetColumn(label, i)
            row.Children.Add(label)
        Next
        _salesRows.Children.Add(row)
    End Sub

    Private Sub AddSummaryRow(values As String(), widths As GridLength())
        Dim row As New Grid With {.MinHeight = 48}
        For Each width As GridLength In widths
            row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = width})
        Next
        For i = 0 To values.Length - 1
            Dim label As New TextBlock With {.Text = values(i), .Foreground = Brush("#34445D"), .FontSize = 11, .Padding = New Thickness(9, 13, 9, 13), .VerticalAlignment = VerticalAlignment.Center, .TextTrimming = TextTrimming.CharacterEllipsis}
            If i = values.Length - 1 Then label.FontWeight = FontWeights.SemiBold
            Grid.SetColumn(label, i)
            row.Children.Add(label)
        Next
        _salesRows.Children.Add(New Border With {.BorderBrush = Brush("#E8EDF3"), .BorderThickness = New Thickness(0, 0, 0, 1), .Child = row})
    End Sub

    Private Function BuildPosContent() As UIElement
        Dim root As New Grid With {.Margin = New Thickness(18)}
        root.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(350)})
        root.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star), .MinWidth = 480})

        Dim cartBorder As New Border With {.Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .CornerRadius = New CornerRadius(13), .Padding = New Thickness(16), .Margin = New Thickness(0, 0, 14, 0)}
        Dim cartLayout As New Grid()
        cartLayout.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        cartLayout.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        cartLayout.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})
        cartLayout.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        cartLayout.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Dim cartHeader As New StackPanel With {.Margin = New Thickness(0, 0, 0, 14)}
        cartHeader.Children.Add(New TextBlock With {.Text = "PUNTO DE VENTA", .Foreground = Brush("#3478D4"), .FontSize = 10, .FontWeight = FontWeights.Bold})
        Dim cartTitle As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 3, 0, 0)}
        cartTitle.Children.Add(New TextBlock With {.Text = "🛒", .FontSize = 22, .Margin = New Thickness(0, 0, 8, 0), .VerticalAlignment = VerticalAlignment.Center})
        cartTitle.Children.Add(New TextBlock With {.Text = "Carrito de compras", .Foreground = Brush("#172B4D"), .FontSize = 21, .FontWeight = FontWeights.SemiBold, .VerticalAlignment = VerticalAlignment.Center})
        cartHeader.Children.Add(cartTitle)
        cartLayout.Children.Add(cartHeader)
        Dim customerArea As New StackPanel With {.Margin = New Thickness(0, 0, 0, 12)}
        customerArea.Children.Add(New TextBlock With {.Text = "CLIENTE", .Foreground = Brush("#718096"), .FontSize = 9, .FontWeight = FontWeights.Bold, .Margin = New Thickness(1, 0, 0, 5)})
        customerArea.Children.Add(_customerInput)
        Grid.SetRow(customerArea, 1)
        cartLayout.Children.Add(customerArea)
        Dim cartScroll As New ScrollViewer With {.VerticalScrollBarVisibility = ScrollBarVisibility.Auto, .HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled}
        cartScroll.Content = _cartPanel
        Grid.SetRow(cartScroll, 2)
        cartLayout.Children.Add(cartScroll)
        Dim totalArea As New StackPanel With {.Margin = New Thickness(0, 12, 0, 8)}
        totalArea.Children.Add(New Border With {.Height = 1, .Background = Brush("#E8EDF3"), .Margin = New Thickness(0, 0, 0, 10)})
        totalArea.Children.Add(New TextBlock With {.Text = "TOTAL A COBRAR", .Foreground = Brush("#718096"), .FontSize = 10, .FontWeight = FontWeights.Bold})
        totalArea.Children.Add(_totalText)
        Grid.SetRow(totalArea, 3)
        cartLayout.Children.Add(totalArea)
        Dim bottom As New StackPanel()
        Dim charge = MakePosButton("Cobrar  →", "#3478D4", Brushes.White)
        charge.Height = 46
        AddHandler charge.Click, AddressOf Charge_Click
        bottom.Children.Add(charge)
        _posStatus.Margin = New Thickness(2, 7, 2, 0)
        bottom.Children.Add(_posStatus)
        Grid.SetRow(bottom, 4)
        cartLayout.Children.Add(bottom)
        cartBorder.Child = cartLayout
        root.Children.Add(cartBorder)

        Dim catalogGrid As New Grid()
        catalogGrid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(155)})
        catalogGrid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim categoryPanel As New Border With {.Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .CornerRadius = New CornerRadius(12), .Padding = New Thickness(9), .Margin = New Thickness(0, 0, 12, 0)}
        Dim categoryLayout As New DockPanel()
        Dim categoryLabel As New TextBlock With {.Text = "▦  CATEGORÍAS", .Foreground = Brush("#718096"), .FontSize = 9, .FontWeight = FontWeights.Bold, .Margin = New Thickness(6, 5, 3, 10)}
        DockPanel.SetDock(categoryLabel, Dock.Top)
        categoryLayout.Children.Add(categoryLabel)
        Dim categoryFrame As New Frame With {.NavigationUIVisibility = NavigationUIVisibility.Hidden, .Background = Brushes.Transparent, .BorderThickness = New Thickness(0), .Content = New Page With {.Background = Brushes.Transparent, .Content = _posCategories}}
        categoryLayout.Children.Add(categoryFrame)
        categoryPanel.Child = categoryLayout
        catalogGrid.Children.Add(categoryPanel)
        Dim catalogPanel As New Border With {.Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .CornerRadius = New CornerRadius(12), .Padding = New Thickness(8)}
        Dim catalogFrame As New Frame With {.NavigationUIVisibility = NavigationUIVisibility.Hidden, .Background = Brushes.Transparent, .BorderThickness = New Thickness(0)}
        Dim catalogScroll As New ScrollViewer With {.VerticalScrollBarVisibility = ScrollBarVisibility.Auto, .HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, .Padding = New Thickness(2)}
        catalogScroll.Content = _catalogGrid
        catalogFrame.Content = New Page With {.Background = Brushes.Transparent, .Content = catalogScroll}
        Dim catalogLayout As New DockPanel()
        Dim menuHeader As New Grid With {.Margin = New Thickness(5, 3, 5, 11)}
        menuHeader.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        menuHeader.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim restaurantLogo = PosRestaurantLogo()
        restaurantLogo.Width = 52
        restaurantLogo.Height = 52
        restaurantLogo.Margin = New Thickness(0, 0, 11, 0)
        restaurantLogo.Stretch = Stretch.Uniform
        menuHeader.Children.Add(restaurantLogo)
        Dim menuTitle As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
        menuTitle.Children.Add(New TextBlock With {.Text = "☷  MENÚ DE OPCIONES", .Foreground = Brush("#172B4D"), .FontSize = 14, .FontWeight = FontWeights.SemiBold})
        menuTitle.Children.Add(New TextBlock With {.Text = "Selecciona un plato para agregarlo a la venta", .Foreground = Brush("#8996A8"), .FontSize = 10, .Margin = New Thickness(0, 3, 0, 0)})
        Grid.SetColumn(menuTitle, 1)
        menuHeader.Children.Add(menuTitle)
        DockPanel.SetDock(menuHeader, Dock.Top)
        catalogLayout.Children.Add(menuHeader)
        catalogLayout.Children.Add(catalogFrame)
        catalogPanel.Child = catalogLayout
        Grid.SetColumn(catalogPanel, 1)
        catalogGrid.Children.Add(catalogPanel)
        Grid.SetColumn(catalogGrid, 1)
        root.Children.Add(catalogGrid)
        Return root
    End Function

    Private Sub LoadPosProducts()
        Try
            _posProducts.Clear()
            For Each values In _posDatabase.GetRows("SELECT ID, Plato, Precio, Categoria, Imagen FROM Platos ORDER BY Plato COLLATE NOCASE")
                Dim price As Double
                Double.TryParse(values(2), NumberStyles.Any, CultureInfo.InvariantCulture, price)
                _posProducts.Add(New PlatoRecord With {.Id = Integer.Parse(values(0), CultureInfo.InvariantCulture), .Nombre = values(1), .Precio = price, .Categoria = values(3), .Imagen = values(4)})
            Next
            RebuildPosCategories()
            RenderPosProducts()
            _posStatus.Text = _posProducts.Count.ToString() & " productos disponibles."
        Catch ex As Exception
            _posStatus.Text = "No se pudo cargar el catálogo: " & ex.Message
            MessageBox.Show("No fue posible cargar los productos del POS." & Environment.NewLine & ex.Message, "NewPOS · POS", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Sub RebuildPosCategories()
        _posCategories.Children.Clear()
        AddPosCategory("Todas", _posProducts.Count)
        For Each category In _posProducts.Select(Function(item) item.Categoria).Where(Function(value) Not String.IsNullOrWhiteSpace(value)).Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(Function(value) value)
            AddPosCategory(category, _posProducts.Where(Function(item) String.Equals(item.Categoria, category, StringComparison.CurrentCultureIgnoreCase)).Count())
        Next
    End Sub

    Private Sub AddPosCategory(category As String, count As Integer)
        Dim active = String.Equals(_selectedCategory, category, StringComparison.CurrentCultureIgnoreCase)
        Dim button = MakePosButton("▦   " & category & "  " & count.ToString(), If(active, "#EAF2FC", "Transparent"), If(active, Brush("#2F66A5"), Brush("#52627A")))
        button.HorizontalContentAlignment = HorizontalAlignment.Left
        button.Padding = New Thickness(8, 10, 4, 10)
        button.Margin = New Thickness(0, 2, 0, 2)
        button.Tag = category
        AddHandler button.Click, AddressOf PosCategory_Click
        _posCategories.Children.Add(button)
    End Sub

    Private Sub RenderPosProducts()
        _catalogGrid.Children.Clear()
        Dim products = If(_selectedCategory = "Todas", _posProducts, _posProducts.Where(Function(item) String.Equals(item.Categoria, _selectedCategory, StringComparison.CurrentCultureIgnoreCase)).ToList())
        For Each product In products
            Dim card As New Button With {.Tag = product.Id, .Padding = New Thickness(0), .Margin = New Thickness(6), .Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .Cursor = Cursors.Hand, .ToolTip = "Agregar " & product.Nombre}
            Dim layout As New StackPanel()
            layout.Children.Add(PosProductImage(product.Imagen))
            Dim labels As New StackPanel With {.Margin = New Thickness(9, 7, 9, 10)}
            labels.Children.Add(New TextBlock With {.Text = product.Nombre, .Foreground = Brush("#263750"), .FontSize = 11, .FontWeight = FontWeights.SemiBold, .TextTrimming = TextTrimming.CharacterEllipsis})
            labels.Children.Add(New TextBlock With {.Text = product.Precio.ToString("C0", CultureInfo.GetCultureInfo("es-CO")), .Foreground = Brush("#3478D4"), .FontSize = 12, .FontWeight = FontWeights.Bold, .Margin = New Thickness(0, 4, 0, 0)})
            layout.Children.Add(labels)
            card.Content = layout
            AddHandler card.Click, AddressOf AddToCart_Click
            _catalogGrid.Children.Add(card)
        Next
        If products.Count = 0 Then _catalogGrid.Children.Add(New TextBlock With {.Text = "No hay productos en esta categoría.", .Foreground = Brush("#718096"), .Margin = New Thickness(20)})
    End Sub

    Private Function PosProductImage(fileName As String) As UIElement
        Dim imagePath = Path.Combine(LocatePosFile("img"), If(fileName, ""))
        If Not String.IsNullOrWhiteSpace(fileName) AndAlso File.Exists(imagePath) Then
            Try
                Dim bitmap As New BitmapImage()
                bitmap.BeginInit()
                bitmap.CacheOption = BitmapCacheOption.OnLoad
                bitmap.UriSource = New Uri(imagePath, UriKind.Absolute)
                bitmap.EndInit()
                Return New Image With {.Source = bitmap, .Height = 100, .Stretch = Stretch.UniformToFill}
            Catch
            End Try
        End If
        Return New Border With {.Height = 100, .Background = Brush("#EFF3F8"), .Child = New TextBlock With {.Text = "▧", .Foreground = Brush("#AAB6C6"), .FontSize = 24, .HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Center}}
    End Function

    Private Function PosRestaurantLogo() As Image
        Dim logoPath = LocatePosFile(Path.Combine("img", "logobroaster.png"))
        If File.Exists(logoPath) Then
            Try
                Dim bitmap As New BitmapImage()
                bitmap.BeginInit()
                bitmap.CacheOption = BitmapCacheOption.OnLoad
                bitmap.UriSource = New Uri(logoPath, UriKind.Absolute)
                bitmap.EndInit()
                Return New Image With {.Source = bitmap}
            Catch
            End Try
        End If
        Return New Image()
    End Function

    Private Sub PosCategory_Click(sender As Object, e As RoutedEventArgs)
        _selectedCategory = CStr(DirectCast(sender, Button).Tag)
        RebuildPosCategories()
        RenderPosProducts()
    End Sub

    Private Sub AddToCart_Click(sender As Object, e As RoutedEventArgs)
        Dim id = CInt(DirectCast(sender, Button).Tag)
        Dim item = _cart.FirstOrDefault(Function(line) line.Producto.Id = id)
        If item Is Nothing Then
            Dim product = _posProducts.FirstOrDefault(Function(candidate) candidate.Id = id)
            If product Is Nothing Then Return
            _cart.Add(New PosCartItem With {.Producto = product, .Cantidad = 1})
        Else
            item.Cantidad += 1
        End If
        RefreshCart()
    End Sub

    Private Sub ChangeQuantity_Click(sender As Object, e As RoutedEventArgs)
        Dim action = DirectCast(DirectCast(sender, Button).Tag, PosCartAction)
        Dim item = _cart.FirstOrDefault(Function(line) line.Producto.Id = action.ProductId)
        If item Is Nothing Then Return
        item.Cantidad += action.Delta
        If item.Cantidad <= 0 Then _cart.Remove(item)
        RefreshCart()
    End Sub

    Private Sub RefreshCart()
        _cartPanel.Children.Clear()
        For Each item In _cart
            Dim row As New Border With {.BorderBrush = Brush("#EDF1F6"), .BorderThickness = New Thickness(0, 0, 0, 1), .Padding = New Thickness(1, 9, 1, 9)}
            Dim grid As New Grid()
            grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
            grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
            Dim info As New StackPanel()
            info.Children.Add(New TextBlock With {.Text = item.Producto.Nombre, .Foreground = Brush("#263750"), .FontSize = 11, .FontWeight = FontWeights.SemiBold, .TextWrapping = TextWrapping.Wrap})
            info.Children.Add(New TextBlock With {.Text = item.Subtotal.ToString("C0", CultureInfo.GetCultureInfo("es-CO")), .Foreground = Brush("#718096"), .FontSize = 10, .Margin = New Thickness(0, 3, 0, 0)})
            grid.Children.Add(info)
            Dim quantity As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
            Dim minus = MakePosButton("−", "#F1F5F9", Brush("#344B6E"))
            minus.Width = 27
            minus.Height = 27
            minus.Tag = New PosCartAction With {.ProductId = item.Producto.Id, .Delta = -1}
            AddHandler minus.Click, AddressOf ChangeQuantity_Click
            quantity.Children.Add(minus)
            quantity.Children.Add(New TextBlock With {.Text = item.Cantidad.ToString(), .Foreground = Brush("#263750"), .FontWeight = FontWeights.SemiBold, .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(8, 0, 8, 0)})
            Dim plus = MakePosButton("+", "#EAF2FC", Brush("#2F66A5"))
            plus.Width = 27
            plus.Height = 27
            plus.Tag = New PosCartAction With {.ProductId = item.Producto.Id, .Delta = 1}
            AddHandler plus.Click, AddressOf ChangeQuantity_Click
            quantity.Children.Add(plus)
            Grid.SetColumn(quantity, 1)
            grid.Children.Add(quantity)
            row.Child = grid
            _cartPanel.Children.Add(row)
        Next
        _totalText.Text = _cart.Sum(Function(item) item.Subtotal).ToString("C0", CultureInfo.GetCultureInfo("es-CO"))
        If _cart.Count = 0 Then _posStatus.Text = "Agrega productos para iniciar una venta."
    End Sub

    Private Sub Charge_Click(sender As Object, e As RoutedEventArgs)
        If _cart.Count = 0 Then
            MessageBox.Show("Agrega al menos un producto antes de cobrar.", "POS", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If
        Dim customer = If(String.IsNullOrWhiteSpace(_customerInput.Text), "Consumidor final", _customerInput.Text.Trim())
        Dim checkout As New CheckoutWindow(customer, _cart.ToList(), Window.GetWindow(Me))
        If checkout.ShowDialog() <> True Then Return
        Try
            Dim saleId = _posDatabase.SavePosSale(checkout.CustomerName, checkout.PaymentMethod, _cart)
            Dim soldItems = _cart.ToList()
            _cart.Clear()
            _customerInput.Clear()
            RefreshCart()
            _posStatus.Text = "Venta #" & saleId.ToString() & " registrada."
            Try
                PrintTicket(saleId, checkout.CustomerName, checkout.PaymentMethod, soldItems)
            Catch printError As Exception
                MessageBox.Show("La venta #" & saleId.ToString() & " quedó registrada, pero no se pudo imprimir el ticket." & Environment.NewLine & printError.Message, "NewPOS · Impresión", MessageBoxButton.OK, MessageBoxImage.Warning)
            End Try
        Catch ex As Exception
            MessageBox.Show("No se pudo completar el cobro." & Environment.NewLine & ex.Message, "NewPOS · POS", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Sub PrintTicket(saleId As Long, customer As String, paymentMethod As String, items As IList(Of PosCartItem))
        Dim document As New FlowDocument With {.PageWidth = 208, .PageHeight = 1100, .PagePadding = New Thickness(9), .ColumnWidth = 190, .FontFamily = New FontFamily("Consolas"), .FontSize = 9}
        document.Blocks.Add(New Paragraph(New Run("TERRENO BROASTER")) With {.TextAlignment = TextAlignment.Center, .FontWeight = FontWeights.Bold, .FontSize = 11})
        document.Blocks.Add(New Paragraph(New Run("TICKET #" & saleId.ToString())) With {.TextAlignment = TextAlignment.Center})
        document.Blocks.Add(New Paragraph(New Run(DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("es-CO")))))
        document.Blocks.Add(New Paragraph(New Run("Cliente: " & customer)))
        document.Blocks.Add(New Paragraph(New Run("Pago: " & paymentMethod)))
        document.Blocks.Add(New Paragraph(New Run("--------------------------------")))
        For Each item In items
            document.Blocks.Add(New Paragraph(New Run(item.Producto.Nombre & " x" & item.Cantidad.ToString())) With {.Margin = New Thickness(0, 3, 0, 0)})
            document.Blocks.Add(New Paragraph(New Run(item.Subtotal.ToString("C0", CultureInfo.GetCultureInfo("es-CO")))) With {.TextAlignment = TextAlignment.Right, .Margin = New Thickness(0)})
        Next
        document.Blocks.Add(New Paragraph(New Run("--------------------------------")))
        document.Blocks.Add(New Paragraph(New Run("TOTAL: " & items.Sum(Function(item) item.Subtotal).ToString("C0", CultureInfo.GetCultureInfo("es-CO")))) With {.FontWeight = FontWeights.Bold, .FontSize = 11, .TextAlignment = TextAlignment.Right})
        document.Blocks.Add(New Paragraph(New Run("¡Gracias por su compra!")) With {.TextAlignment = TextAlignment.Center, .Margin = New Thickness(0, 10, 0, 0)})
        Dim printDialog As New PrintDialog()
        Try
            printDialog.PrintTicket.PageMediaSize = New PageMediaSize(PageMediaSizeName.Unknown, 208, 1100)
        Catch
            ' Algunos controladores térmicos solo exponen sus tamaños de papel predefinidos.
        End Try
        If printDialog.ShowDialog() <> True Then Return
        Dim paginator = CType(document, IDocumentPaginatorSource).DocumentPaginator
        paginator.PageSize = New Size(208, 1100)
        printDialog.PrintDocument(paginator, "Ticket POS #" & saleId.ToString())
    End Sub

    Private Shared Function LocatePosFile(relativePath As String) As String
        Dim baseDirectory = AppDomain.CurrentDomain.BaseDirectory
        Dim deployedPath = Path.Combine(baseDirectory, relativePath)
        Dim projectPath = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", relativePath))
        If File.Exists(deployedPath) OrElse Directory.Exists(deployedPath) Then Return deployedPath
        If File.Exists(projectPath) OrElse Directory.Exists(projectPath) Then Return projectPath
        Return deployedPath
    End Function

    Private Function MakePosButton(label As String, background As String, foreground As Brush) As Button
        Return New Button With {.Content = label, .Background = Brush(background), .Foreground = foreground, .BorderThickness = New Thickness(0), .Padding = New Thickness(8), .Margin = New Thickness(2), .FontSize = 11, .FontWeight = FontWeights.SemiBold, .Cursor = Cursors.Hand}
    End Function

    Private Function BuildHome() As UIElement
        Dim root As New StackPanel With {.Margin = New Thickness(34, 30, 34, 24)}
        root.Children.Add(New TextBlock With {.Text = "INICIO", .Foreground = Brush("#3478D4"), .FontSize = 11, .FontWeight = FontWeights.SemiBold})
        root.Children.Add(New TextBlock With {.Text = "Todo tu negocio, en un solo lugar.", .Foreground = Brush("#172B4D"), .FontSize = 30, .FontWeight = FontWeights.SemiBold, .Margin = New Thickness(0, 8, 0, 8)})
        root.Children.Add(New TextBlock With {.Text = "NewPOS te ayuda a atender, vender y entender el movimiento de tu punto de venta desde una experiencia clara y sencilla.", .Foreground = Brush("#718096"), .FontSize = 14, .TextWrapping = TextWrapping.Wrap, .MaxWidth = 760, .Margin = New Thickness(0, 0, 0, 24)})
        Dim hero As New Border With {.Background = Brush("#1D3557"), .CornerRadius = New CornerRadius(16), .Padding = New Thickness(28), .Margin = New Thickness(0, 0, 0, 20)}
        Dim heroStack As New StackPanel()
        heroStack.Children.Add(New TextBlock With {.Text = "PUNTO DE VENTA · NEWPOS", .Foreground = Brush("#9CC4FF"), .FontSize = 10, .FontWeight = FontWeights.Bold})
        heroStack.Children.Add(New TextBlock With {.Text = "Vende con agilidad.\nDecide con información.", .Foreground = Brushes.White, .FontSize = 28, .FontWeight = FontWeights.SemiBold, .Margin = New Thickness(0, 10, 0, 9)})
        heroStack.Children.Add(New TextBlock With {.Text = "Accede al POS para iniciar una venta o explora el dashboard para conocer el estado de tu operación.", .Foreground = Brush("#C7D4E7"), .FontSize = 13, .TextWrapping = TextWrapping.Wrap})
        hero.Child = heroStack
        root.Children.Add(hero)
        Dim cards As New UniformGrid With {.Columns = 3}
        cards.Children.Add(FeatureCard("▣", "Venta rápida", "Un flujo de cobro claro para atender a tus clientes con menos pasos."))
        cards.Children.Add(FeatureCard("◫", "Visión del negocio", "Consulta indicadores y tendencias desde el dashboard."))
        cards.Children.Add(FeatureCard("▤", "Control centralizado", "Administra productos, usuarios y reportes desde un mismo lugar."))
        root.Children.Add(cards)
        root.Children.Add(New TextBlock With {.Text = "Comienza desde el menú lateral o usa los accesos rápidos de la barra superior.", .Foreground = Brush("#718096"), .FontSize = 12, .Margin = New Thickness(2, 20, 0, 0)})
        Return New ScrollViewer With {.VerticalScrollBarVisibility = ScrollBarVisibility.Auto, .Content = root}
    End Function

    Private Function BuildDashboard() As UIElement
        Dim root As New StackPanel With {.Margin = New Thickness(24, 20, 24, 28)}
        Dim header As New Grid()
        header.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        header.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim titleStack As New StackPanel()
        titleStack.Children.Add(New TextBlock With {.Text = "ANÁLISIS DEL NEGOCIO", .Foreground = Brush("#3478D4"), .FontSize = 10, .FontWeight = FontWeights.Bold})
        titleStack.Children.Add(New TextBlock With {.Text = "Dashboard", .Foreground = Brush("#172B4D"), .FontSize = 27, .FontWeight = FontWeights.SemiBold, .Margin = New Thickness(0, 3, 0, 0)})
        titleStack.Children.Add(New TextBlock With {.Text = "Ventas, productos y formas de pago en un solo vistazo.", .Foreground = Brush("#718096"), .FontSize = 12, .Margin = New Thickness(0, 2, 0, 0)})
        header.Children.Add(titleStack)
        Dim filters As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
        Dim previousWeek = MakePosButton("‹", "#FFFFFF", Brush("#344B6E"))
        previousWeek.Width = 34
        previousWeek.Height = 34
        previousWeek.ToolTip = "Semana anterior"
        AddHandler previousWeek.Click, Sub(sender, args) ShiftDashboardWeek(-7)
        filters.Children.Add(previousWeek)
        filters.Children.Add(_dashboardWeekLabel)
        Dim nextWeek = MakePosButton("›", "#FFFFFF", Brush("#344B6E"))
        nextWeek.Width = 34
        nextWeek.Height = 34
        nextWeek.ToolTip = "Semana siguiente"
        AddHandler nextWeek.Click, Sub(sender, args) ShiftDashboardWeek(7)
        filters.Children.Add(nextWeek)
        Dim calendarWrap As New StackPanel With {.Margin = New Thickness(14, 0, 0, 0)}
        calendarWrap.Children.Add(New TextBlock With {.Text = "FECHA DE CONSULTA", .Foreground = Brush("#718096"), .FontSize = 9, .FontWeight = FontWeights.Bold, .Margin = New Thickness(1, 0, 0, 3)})
        calendarWrap.Children.Add(_dashboardDatePicker)
        filters.Children.Add(calendarWrap)
        Grid.SetColumn(filters, 1)
        header.Children.Add(filters)
        root.Children.Add(header)
        root.Children.Add(_dashboardKpis)

        Dim charts As New Grid()
        charts.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1.35, GridUnitType.Star)})
        charts.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        charts.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(285)})
        charts.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(285)})
        charts.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(260)})

        _salesAreaChart.Height = 225
        _paymentPieChart.Height = 225
        _productBarChart.Height = 225
        _categoryPieChart.Height = 205
        Dim areaPanel = DashboardChartPanel("◒", "Ventas diarias", "Suma de ventas por día · semana seleccionada", _salesAreaChart)
        Dim paymentPanel = DashboardChartPanel("◉", "Formas de pago", "Distribución por monto · mes de la fecha elegida", _paymentPieChart)
        Dim productPanel = DashboardChartPanel("▥", "Productos más vendidos", "Unidades vendidas · mes de la fecha elegida", _productBarChart)
        Dim goalPanel = DashboardChartPanel("◔", "Meta diaria", "Objetivo de venta: $1.000.000", GoalGaugeContent())
        Dim categoryPanel = DashboardChartPanel("◉", "Ventas por categoría", "Participación de cada categoría · mes seleccionado", _categoryPieChart)
        Grid.SetRow(areaPanel, 0) : Grid.SetColumn(areaPanel, 0)
        Grid.SetRow(paymentPanel, 0) : Grid.SetColumn(paymentPanel, 1)
        Grid.SetRow(productPanel, 1) : Grid.SetColumn(productPanel, 0)
        Grid.SetRow(goalPanel, 1) : Grid.SetColumn(goalPanel, 1)
        Grid.SetRow(categoryPanel, 2) : Grid.SetColumnSpan(categoryPanel, 2)
        charts.Children.Add(areaPanel)
        charts.Children.Add(paymentPanel)
        charts.Children.Add(productPanel)
        charts.Children.Add(goalPanel)
        charts.Children.Add(categoryPanel)
        root.Children.Add(charts)
        _dashboardStatus.Margin = New Thickness(2, 9, 0, 0)
        root.Children.Add(_dashboardStatus)
        Return New ScrollViewer With {.VerticalScrollBarVisibility = ScrollBarVisibility.Auto, .HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, .Content = root}
    End Function

    Private Function GoalGaugeContent() As UIElement
        ' AngularGauge puede bloquear el hilo de interfaz durante la primera medida
        ' cuando se combina con varios charts dentro de un ScrollViewer en WPF.
        ' Esta meta usa controles WPF ligeros para mantener el dashboard responsivo.
        _goalProgress = New ProgressBar With {.Minimum = 0, .Maximum = 1000000, .Height = 18, .Foreground = Brush("#3478D4"), .Background = Brush("#EAF0F7"), .Margin = New Thickness(18, 0, 18, 0), .VerticalAlignment = VerticalAlignment.Center}
        _goalValueText.Text = "$0 / $1.000.000"
        _goalValueText.Foreground = Brush("#263750")
        _goalValueText.FontSize = 13
        _goalValueText.FontWeight = FontWeights.Bold
        _goalValueText.HorizontalAlignment = HorizontalAlignment.Center
        _goalValueText.VerticalAlignment = VerticalAlignment.Center
        _goalValueText.Margin = New Thickness(0, 14, 0, 0)
        Dim content As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
        content.Children.Add(_goalProgress)
        content.Children.Add(_goalValueText)
        Return content
    End Function

    Private Function DashboardChartPanel(icon As String, title As String, subtitle As String, chart As UIElement) As UIElement
        Dim panel As New Border With {.Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .CornerRadius = New CornerRadius(12), .Padding = New Thickness(13), .Margin = New Thickness(0, 0, 12, 12)}
        Dim layout As New DockPanel()
        Dim heading As New StackPanel With {.Margin = New Thickness(2, 0, 2, 7)}
        heading.Children.Add(New TextBlock With {.Text = icon & "  " & title, .Foreground = Brush("#263750"), .FontSize = 13, .FontWeight = FontWeights.SemiBold})
        heading.Children.Add(New TextBlock With {.Text = subtitle, .Foreground = Brush("#8996A8"), .FontSize = 9, .Margin = New Thickness(0, 3, 0, 0)})
        DockPanel.SetDock(heading, Dock.Top)
        layout.Children.Add(heading)
        layout.Children.Add(chart)
        panel.Child = layout
        Return panel
    End Function

    Private Function DashboardKpi(icon As String, title As String, value As String, detail As String, accent As String) As UIElement
        Dim panel As New Border With {.Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .CornerRadius = New CornerRadius(12), .Padding = New Thickness(15), .Margin = New Thickness(0, 0, 12, 0)}
        Dim layout As New Grid()
        layout.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(43)})
        layout.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim iconBorder As New Border With {.Width = 36, .Height = 36, .CornerRadius = New CornerRadius(10), .Background = Brush(accent), .VerticalAlignment = VerticalAlignment.Top}
        iconBorder.Child = New TextBlock With {.Text = icon, .Foreground = Brush("#FFFFFF"), .FontSize = 18, .HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Center}
        layout.Children.Add(iconBorder)
        Dim text As New StackPanel With {.Margin = New Thickness(4, 0, 0, 0)}
        text.Children.Add(New TextBlock With {.Text = title.ToUpperInvariant(), .Foreground = Brush("#718096"), .FontSize = 9, .FontWeight = FontWeights.Bold, .TextTrimming = TextTrimming.CharacterEllipsis})
        text.Children.Add(New TextBlock With {.Text = value, .Foreground = Brush("#172B4D"), .FontSize = 19, .FontWeight = FontWeights.Bold, .TextTrimming = TextTrimming.CharacterEllipsis, .Margin = New Thickness(0, 5, 0, 2)})
        text.Children.Add(New TextBlock With {.Text = detail, .Foreground = Brush("#8996A8"), .FontSize = 9, .TextWrapping = TextWrapping.Wrap})
        Grid.SetColumn(text, 1)
        layout.Children.Add(text)
        panel.Child = layout
        Return panel
    End Function

    Private Sub DashboardDate_Changed(sender As Object, e As SelectionChangedEventArgs)
        RefreshDashboard()
    End Sub

    Private Sub Dashboard_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Me.Loaded, AddressOf Dashboard_Loaded
        InitializeDashboard()
    End Sub

    Private Function DashboardLoadingView() As UIElement
        Dim message As New StackPanel With {.HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Center}
        message.Children.Add(New TextBlock With {.Text = "◌", .Foreground = Brush("#3478D4"), .FontSize = 38, .HorizontalAlignment = HorizontalAlignment.Center})
        message.Children.Add(New TextBlock With {.Text = "Preparando dashboard…", .Foreground = Brush("#52627A"), .FontSize = 14, .FontWeight = FontWeights.SemiBold, .HorizontalAlignment = HorizontalAlignment.Center, .Margin = New Thickness(0, 7, 0, 0)})
        Dim loadingGrid As New Grid With {.Background = Brush("#F3F6FA")}
        loadingGrid.Children.Add(message)
        Return loadingGrid
    End Function

    Private Sub InitializeDashboard()
        Try
            _dashboardDatePicker = New DatePicker With {.SelectedDate = Date.Today, .Width = 155, .Height = 34, .SelectedDateFormat = DatePickerFormat.Short, .ToolTip = "Elige el día que quieres analizar"}
            _dashboardWeekLabel = New TextBlock With {.Foreground = Brush("#52627A"), .FontSize = 11, .FontWeight = FontWeights.SemiBold, .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(8, 0, 8, 0)}
            _dashboardStatus = New TextBlock With {.Foreground = Brush("#718096"), .FontSize = 10, .VerticalAlignment = VerticalAlignment.Center}
            _dashboardKpis = New UniformGrid With {.Columns = 4, .Margin = New Thickness(0, 17, 0, 17)}
            _salesAreaChart = New CartesianChart()
            _paymentPieChart = New PieChart()
            _categoryPieChart = New PieChart()
            _productBarChart = New CartesianChart()
            _goalValueText = New TextBlock()
            AddHandler _dashboardDatePicker.SelectedDateChanged, AddressOf DashboardDate_Changed
            Content = BuildDashboard()
            RefreshDashboard()
        Catch ex As Exception
            Dim errorText As New TextBlock With {.Text = "No se pudo iniciar el dashboard: " & ex.Message, .Foreground = Brush("#B42318"), .FontSize = 13, .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(24)}
            Content = New Border With {.Background = Brushes.White, .CornerRadius = New CornerRadius(12), .Margin = New Thickness(25), .Padding = New Thickness(10), .Child = errorText}
        End Try
    End Sub

    Private Sub ShiftDashboardWeek(days As Integer)
        Dim selected = If(_dashboardDatePicker.SelectedDate, Date.Today)
        _dashboardDatePicker.SelectedDate = selected.AddDays(days)
    End Sub

    Private Async Sub RefreshDashboard()
        If _dashboardDatePicker Is Nothing Then Return
        Dim selected = If(_dashboardDatePicker.SelectedDate, Date.Today).Date
        Dim monthStart As New Date(selected.Year, selected.Month, 1)
        Dim requestVersion = System.Threading.Interlocked.Increment(_dashboardRefreshVersion)
        _dashboardStatus.Text = "Cargando información del período…"
        Dim daySales As List(Of DashboardSale)
        Dim monthSales As List(Of DashboardSale)
        Dim dayDetails As List(Of DashboardDetail)
        Dim monthDetails As List(Of DashboardDetail)
        Try
            Dim snapshot = Await Task.Run(Function() New DashboardSnapshot With {.Sales = ReadDashboardSales(), .Details = ReadDashboardDetails()})
            If requestVersion <> System.Threading.Interlocked.CompareExchange(_dashboardRefreshVersion, 0, 0) Then Return
            _dashboardSales = snapshot.Sales
            _dashboardDetails = snapshot.Details
            daySales = _dashboardSales.Where(Function(sale) sale.Fecha.Date = selected).ToList()
            monthSales = _dashboardSales.Where(Function(sale) sale.Fecha >= monthStart AndAlso sale.Fecha < monthStart.AddMonths(1)).ToList()
            dayDetails = _dashboardDetails.Where(Function(item) item.Fecha.Date = selected).ToList()
            monthDetails = _dashboardDetails.Where(Function(item) item.Fecha >= monthStart AndAlso item.Fecha < monthStart.AddMonths(1)).ToList()
            _dashboardStatus.Text = "Datos actualizados: " & DateTime.Now.ToString("HH:mm")
        Catch ex As Exception
            If requestVersion <> System.Threading.Interlocked.CompareExchange(_dashboardRefreshVersion, 0, 0) Then Return
            _dashboardStatus.Text = "No se pudieron leer Ventas y Detalles: " & ex.Message
            Return
        End Try

        Dim dailyTop = TopDashboardProduct(dayDetails)
        Dim monthlyTop = TopDashboardProduct(monthDetails)
        Dim mostUsedPayment = daySales.GroupBy(Function(sale) If(String.IsNullOrWhiteSpace(sale.FormaPago), "Sin especificar", sale.FormaPago)).OrderByDescending(Function(group) group.Count()).ThenBy(Function(group) group.Key).FirstOrDefault()
        Dim paymentText = If(mostUsedPayment Is Nothing, "Sin ventas", mostUsedPayment.Key)
        Dim paymentDetail = If(mostUsedPayment Is Nothing, "Sin pagos registrados", mostUsedPayment.Count().ToString() & " pagos · " & mostUsedPayment.Sum(Function(sale) sale.Monto).ToString("C0", CultureInfo.GetCultureInfo("es-CO")))
        _dashboardKpis.Children.Clear()
        _dashboardKpis.Children.Add(DashboardKpi("▤", "Número de ventas", daySales.Count.ToString(), "Mes: " & monthSales.Count.ToString() & " ventas", "#3478D4"))
        _dashboardKpis.Children.Add(DashboardKpi("$", "Ventas acumuladas", daySales.Sum(Function(sale) sale.Monto).ToString("C0", CultureInfo.GetCultureInfo("es-CO")), "Mes: " & monthSales.Sum(Function(sale) sale.Monto).ToString("C0", CultureInfo.GetCultureInfo("es-CO")), "#20A779"))
        _dashboardKpis.Children.Add(DashboardKpi("★", "Producto más vendido", dailyTop, "Mes: " & monthlyTop, "#F0A52B"))
        _dashboardKpis.Children.Add(DashboardKpi("▣", "Forma de pago principal", paymentText, paymentDetail, "#805AD5"))

        UpdateDashboardCharts(selected, monthSales, monthDetails)
    End Sub

    Private Function ReadDashboardSales() As List(Of DashboardSale)
        Dim result As New List(Of DashboardSale)()
        For Each row In _dashboardDatabase.GetRows("SELECT Fecha, NTicket, Cliente, Monto, FormaPago FROM Ventas")
            Dim saleDate As DateTime
            Dim amount As Double
            If TryReadDate(row(0), saleDate) Then
                Double.TryParse(row(3), NumberStyles.Any, CultureInfo.InvariantCulture, amount)
                result.Add(New DashboardSale With {.Fecha = saleDate, .NTicket = row(1), .Cliente = row(2), .Monto = amount, .FormaPago = row(4)})
            End If
        Next
        Return result
    End Function

    Private Function ReadDashboardDetails() As List(Of DashboardDetail)
        Dim result As New List(Of DashboardDetail)()
        For Each row In _dashboardDatabase.GetRows("SELECT Fecha, NTicket, Plato, Precio, Cantidad, Categoria, Importe FROM Detalles")
            Dim detailDate As DateTime
            Dim price As Double
            Dim quantity As Integer
            Dim amount As Double
            If TryReadDate(row(0), detailDate) Then
                Double.TryParse(row(3), NumberStyles.Any, CultureInfo.InvariantCulture, price)
                Integer.TryParse(row(4), NumberStyles.Any, CultureInfo.InvariantCulture, quantity)
                Double.TryParse(row(6), NumberStyles.Any, CultureInfo.InvariantCulture, amount)
                result.Add(New DashboardDetail With {.Fecha = detailDate, .NTicket = row(1), .Plato = row(2), .Precio = price, .Cantidad = quantity, .Categoria = row(5), .Importe = amount})
            End If
        Next
        Return result
    End Function

    Private Shared Function TryReadDate(value As String, ByRef parsed As DateTime) As Boolean
        Return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, parsed) OrElse DateTime.TryParse(value, CultureInfo.GetCultureInfo("es-CO"), DateTimeStyles.AssumeLocal, parsed)
    End Function

    Private Shared Function TopDashboardProduct(items As IEnumerable(Of DashboardDetail)) As String
        Dim top = items.Where(Function(item) Not String.IsNullOrWhiteSpace(item.Plato)).GroupBy(Function(item) item.Plato).Select(Function(group) New With {.Nombre = group.Key, .Cantidad = group.Sum(Function(item) item.Cantidad)}).OrderByDescending(Function(item) item.Cantidad).ThenBy(Function(item) item.Nombre).FirstOrDefault()
        Return If(top Is Nothing, "Sin ventas", top.Nombre & " · " & top.Cantidad.ToString() & " u.")
    End Function

    Private Sub UpdateDashboardCharts(selected As Date, monthSales As IList(Of DashboardSale), monthDetails As IList(Of DashboardDetail))
        Dim offset = (CInt(selected.DayOfWeek) + 6) Mod 7
        Dim weekStart = selected.AddDays(-offset)
        Dim weekEnd = weekStart.AddDays(6)
        _dashboardWeekLabel.Text = weekStart.ToString("dd MMM", CultureInfo.GetCultureInfo("es-CO")) & " – " & weekEnd.ToString("dd MMM", CultureInfo.GetCultureInfo("es-CO"))
        Dim dates = Enumerable.Range(0, 7).Select(Function(index) weekStart.AddDays(index)).ToList()
        Dim areaValues = New ChartValues(Of Double)(dates.Select(Function(day) _dashboardSales.Where(Function(sale) sale.Fecha.Date = day.Date).Sum(Function(sale) sale.Monto)))
        Dim labels = dates.Select(Function(day) day.ToString("ddd dd", CultureInfo.GetCultureInfo("es-CO"))).ToList()
        _salesAreaChart.DisableAnimations = True
        Dim areaSeries As New SeriesCollection()
        areaSeries.Add(New LineSeries With {.Title = "Ventas", .Values = areaValues, .Fill = Brush("#553478D4"), .Stroke = Brush("#3478D4"), .PointGeometrySize = 6, .LineSmoothness = 0.35})
        _salesAreaChart.Series = areaSeries
        _salesAreaChart.AxisX.Clear()
        _salesAreaChart.AxisX.Add(New Axis With {.Labels = labels, .FontSize = 9, .Foreground = Brush("#718096")})
        _salesAreaChart.AxisY.Clear()
        _salesAreaChart.AxisY.Add(New Axis With {.MinValue = 0, .LabelFormatter = Function(value) (value / 1000000.0).ToString("0.0", CultureInfo.InvariantCulture) & " M", .FontSize = 9, .Foreground = Brush("#718096")})
        _salesAreaChart.LegendLocation = LegendLocation.None

        Dim paymentGroups = monthSales.GroupBy(Function(sale) If(String.IsNullOrWhiteSpace(sale.FormaPago), "Sin especificar", sale.FormaPago)).Select(Function(group) New With {.Nombre = group.Key, .Monto = group.Sum(Function(sale) sale.Monto)}).Where(Function(item) item.Monto > 0).OrderByDescending(Function(item) item.Monto).ToList()
        _paymentPieChart.DisableAnimations = True
        Dim paymentSeries As New SeriesCollection()
        For Each payment In paymentGroups
            paymentSeries.Add(New PieSeries With {.Title = payment.Nombre, .Values = New ChartValues(Of Double) From {payment.Monto}, .DataLabels = True})
        Next
        _paymentPieChart.Series = paymentSeries
        _paymentPieChart.LegendLocation = LegendLocation.Right

        Dim topProducts = monthDetails.Where(Function(item) Not String.IsNullOrWhiteSpace(item.Plato)).GroupBy(Function(item) item.Plato).Select(Function(group) New With {.Nombre = group.Key, .Cantidad = group.Sum(Function(item) item.Cantidad)}).OrderByDescending(Function(item) item.Cantidad).Take(5).ToList()
        _productBarChart.DisableAnimations = True
        Dim barSeries As New SeriesCollection()
        barSeries.Add(New ColumnSeries With {.Title = "Unidades", .Values = New ChartValues(Of Double)(topProducts.Select(Function(item) CDbl(item.Cantidad))), .Fill = Brush("#20A779")})
        _productBarChart.Series = barSeries
        _productBarChart.AxisX.Clear()
        _productBarChart.AxisX.Add(New Axis With {.Labels = topProducts.Select(Function(item) item.Nombre).ToList(), .LabelsRotation = 18, .FontSize = 9, .Foreground = Brush("#718096")})
        _productBarChart.AxisY.Clear()
        _productBarChart.AxisY.Add(New Axis With {.MinValue = 0, .FontSize = 9, .Foreground = Brush("#718096")})
        _productBarChart.LegendLocation = LegendLocation.None

        Dim dailyTotal = _dashboardSales.Where(Function(sale) sale.Fecha.Date = selected.Date).Sum(Function(sale) sale.Monto)
        If _goalProgress IsNot Nothing Then _goalProgress.Value = Math.Min(1000000, dailyTotal)
        Dim percent = dailyTotal / 1000000.0
        _goalValueText.Text = dailyTotal.ToString("C0", CultureInfo.GetCultureInfo("es-CO")) & " / $1.000.000" & Environment.NewLine & percent.ToString("P0", CultureInfo.GetCultureInfo("es-CO"))

        Dim categoryGroups = monthDetails.GroupBy(Function(item) If(String.IsNullOrWhiteSpace(item.Categoria), "Sin categoría", item.Categoria)).Select(Function(group) New With {.Nombre = group.Key, .Monto = group.Sum(Function(item) item.Importe)}).Where(Function(item) item.Monto > 0).OrderByDescending(Function(item) item.Monto).ToList()
        _categoryPieChart.DisableAnimations = True
        Dim categorySeries As New SeriesCollection()
        For Each category In categoryGroups
            categorySeries.Add(New PieSeries With {.Title = category.Nombre, .Values = New ChartValues(Of Double) From {category.Monto}, .DataLabels = True})
        Next
        _categoryPieChart.Series = categorySeries
        _categoryPieChart.LegendLocation = LegendLocation.Right
    End Sub

    Private Function BuildDestination(pageName As String) As UIElement
        Dim root As New StackPanel With {.Margin = New Thickness(36, 34, 36, 24)}
        root.Children.Add(New TextBlock With {.Text = "NEWPOS  /  " & pageName.ToUpperInvariant(), .Foreground = Brush("#3478D4"), .FontSize = 10, .FontWeight = FontWeights.SemiBold})
        root.Children.Add(New TextBlock With {.Text = pageName, .Foreground = Brush("#172B4D"), .FontSize = 29, .FontWeight = FontWeights.SemiBold, .Margin = New Thickness(0, 9, 0, 5)})
        root.Children.Add(New TextBlock With {.Text = "Espacio de trabajo para " & pageName.ToLowerInvariant() & ".", .Foreground = Brush("#718096"), .FontSize = 14, .Margin = New Thickness(0, 0, 0, 24)})
        Dim card As New Border With {.Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .CornerRadius = New CornerRadius(13), .Padding = New Thickness(23), .MaxWidth = 850, .HorizontalAlignment = HorizontalAlignment.Left}
        Dim inside As New StackPanel()
        inside.Children.Add(New TextBlock With {.Text = "Panel de " & pageName, .Foreground = Brush("#263750"), .FontSize = 16, .FontWeight = FontWeights.SemiBold})
        inside.Children.Add(New TextBlock With {.Text = "La navegación está lista. Aquí se incorporarán las herramientas, filtros y datos correspondientes a esta sección.", .Foreground = Brush("#718096"), .FontSize = 13, .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 9, 0, 0), .MaxWidth = 720})
        card.Child = inside
        root.Children.Add(card)
        Return New ScrollViewer With {.VerticalScrollBarVisibility = ScrollBarVisibility.Auto, .Content = root}
    End Function

    Private Function FeatureCard(icon As String, title As String, description As String) As UIElement
        Dim border As New Border With {.Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .CornerRadius = New CornerRadius(12), .Padding = New Thickness(17), .Margin = New Thickness(0, 0, 12, 0)}
        Dim stack As New StackPanel()
        stack.Children.Add(New TextBlock With {.Text = icon, .Foreground = Brush("#3478D4"), .FontSize = 20})
        stack.Children.Add(New TextBlock With {.Text = title, .Foreground = Brush("#263750"), .FontSize = 14, .FontWeight = FontWeights.SemiBold, .Margin = New Thickness(0, 9, 0, 5)})
        stack.Children.Add(New TextBlock With {.Text = description, .Foreground = Brush("#718096"), .FontSize = 11, .TextWrapping = TextWrapping.Wrap})
        border.Child = stack
        Return border
    End Function

    Private Function StatCard(title As String, value As String, hint As String) As UIElement
        Dim border As New Border With {.Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .CornerRadius = New CornerRadius(11), .Padding = New Thickness(16), .Margin = New Thickness(0, 0, 11, 0)}
        Dim stack As New StackPanel()
        stack.Children.Add(New TextBlock With {.Text = title, .Foreground = Brush("#718096"), .FontSize = 9, .FontWeight = FontWeights.SemiBold})
        stack.Children.Add(New TextBlock With {.Text = value, .Foreground = Brush("#172B4D"), .FontSize = 23, .FontWeight = FontWeights.SemiBold, .Margin = New Thickness(0, 7, 0, 2)})
        stack.Children.Add(New TextBlock With {.Text = hint, .Foreground = Brush("#97A3B5"), .FontSize = 10})
        border.Child = stack
        Return border
    End Function

    Private Function Brush(hexColor As String) As SolidColorBrush
        Return New SolidColorBrush(CType(ColorConverter.ConvertFromString(hexColor), Color))
    End Function
End Class

Public Class PosCartItem
    Public Property Producto As PlatoRecord
    Public Property Cantidad As Integer
    Public ReadOnly Property Subtotal As Double
        Get
            Return If(Producto Is Nothing, 0, Producto.Precio * Cantidad)
        End Get
    End Property
End Class

Public Class PosCartAction
    Public Property ProductId As Integer
    Public Property Delta As Integer
End Class

Public Class DashboardSale
    Public Property Fecha As DateTime
    Public Property NTicket As String
    Public Property Cliente As String
    Public Property Monto As Double
    Public Property FormaPago As String
End Class

Public Class DashboardSnapshot
    Public Property Sales As List(Of DashboardSale)
    Public Property Details As List(Of DashboardDetail)
End Class

Public Class DashboardDetail
    Public Property Fecha As DateTime
    Public Property NTicket As String
    Public Property Plato As String
    Public Property Precio As Double
    Public Property Cantidad As Integer
    Public Property Categoria As String
    Public Property Importe As Double
End Class

Public Class CheckoutWindow
    Inherits Window

    Private ReadOnly _customer As TextBox
    Private ReadOnly _payment As ComboBox

    Public ReadOnly Property CustomerName As String
        Get
            Return If(String.IsNullOrWhiteSpace(_customer.Text), "Consumidor final", _customer.Text.Trim())
        End Get
    End Property

    Public ReadOnly Property PaymentMethod As String
        Get
            Return If(_payment.SelectedItem Is Nothing, "Efectivo", CStr(_payment.SelectedItem))
        End Get
    End Property

    Public Sub New(customerName As String, items As IList(Of PosCartItem), ownerWindow As Window)
        Title = "Confirmar cobro · NewPOS"
        Width = 480
        Height = 620
        MinHeight = 540
        WindowStartupLocation = If(ownerWindow Is Nothing, WindowStartupLocation.CenterScreen, WindowStartupLocation.CenterOwner)
        If ownerWindow IsNot Nothing Then Owner = ownerWindow
        ResizeMode = ResizeMode.NoResize
        Background = New SolidColorBrush(Color.FromRgb(&HF3, &HF6, &HFA))
        FontFamily = New FontFamily("Segoe UI")
        _customer = New TextBox With {.Text = customerName, .Height = 38, .Padding = New Thickness(10), .FontSize = 13, .BorderBrush = New SolidColorBrush(Color.FromRgb(&HD8, &HE1, &HEC)), .BorderThickness = New Thickness(1)}
        _payment = New ComboBox With {.Height = 38, .FontSize = 13, .Padding = New Thickness(7)}
        For Each methodName In {"Efectivo", "Punto de Venta", "Pago Móvil", "Transferencia"}
            _payment.Items.Add(methodName)
        Next
        _payment.SelectedIndex = 0
        Content = BuildCheckout(items)
    End Sub

    Private Function BuildCheckout(items As IList(Of PosCartItem)) As UIElement
        Dim root As New Grid With {.Margin = New Thickness(24)}
        root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        root.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})
        root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Dim heading As New StackPanel With {.Margin = New Thickness(0, 0, 0, 18)}
        heading.Children.Add(New TextBlock With {.Text = "CONFIRMAR VENTA", .Foreground = New SolidColorBrush(Color.FromRgb(&H34, &H78, &HD4)), .FontSize = 10, .FontWeight = FontWeights.Bold})
        heading.Children.Add(New TextBlock With {.Text = "Revisa los datos del cobro", .Foreground = New SolidColorBrush(Color.FromRgb(&H23, &H43, &H6D)), .FontSize = 22, .FontWeight = FontWeights.SemiBold, .Margin = New Thickness(0, 4, 0, 0)})
        root.Children.Add(heading)
        Dim customerArea As New StackPanel With {.Margin = New Thickness(0, 0, 0, 13)}
        customerArea.Children.Add(New TextBlock With {.Text = "CLIENTE", .Foreground = New SolidColorBrush(Color.FromRgb(&H71, &H80, &H96)), .FontSize = 9, .FontWeight = FontWeights.Bold, .Margin = New Thickness(1, 0, 0, 5)})
        customerArea.Children.Add(_customer)
        Grid.SetRow(customerArea, 1)
        root.Children.Add(customerArea)
        Dim itemScroll As New ScrollViewer With {.VerticalScrollBarVisibility = ScrollBarVisibility.Auto, .Margin = New Thickness(0, 0, 0, 12)}
        Dim itemList As New StackPanel()
        For Each item In items
            itemList.Children.Add(New TextBlock With {.Text = item.Producto.Nombre & "  × " & item.Cantidad.ToString() & "     " & item.Subtotal.ToString("C0", CultureInfo.GetCultureInfo("es-CO")), .Foreground = New SolidColorBrush(Color.FromRgb(&H38, &H4B, &H65)), .FontSize = 11, .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 7, 0, 7)})
            itemList.Children.Add(New Border With {.Height = 1, .Background = New SolidColorBrush(Color.FromRgb(&HE8, &HED, &HF3))})
        Next
        itemScroll.Content = itemList
        Grid.SetRow(itemScroll, 2)
        root.Children.Add(itemScroll)
        Dim paymentArea As New StackPanel With {.Margin = New Thickness(0, 4, 0, 14)}
        paymentArea.Children.Add(New TextBlock With {.Text = "FORMA DE PAGO", .Foreground = New SolidColorBrush(Color.FromRgb(&H71, &H80, &H96)), .FontSize = 9, .FontWeight = FontWeights.Bold, .Margin = New Thickness(1, 0, 0, 5)})
        paymentArea.Children.Add(_payment)
        Grid.SetRow(paymentArea, 3)
        root.Children.Add(paymentArea)
        Dim actions As New Grid()
        actions.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        actions.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim cancel As New Button With {.Content = "Cancelar", .Height = 42, .Margin = New Thickness(0, 0, 5, 0), .Background = Brushes.White, .Foreground = New SolidColorBrush(Color.FromRgb(&H52, &H62, &H7A)), .BorderBrush = New SolidColorBrush(Color.FromRgb(&HE4, &HEA, &HF1)), .BorderThickness = New Thickness(1), .Cursor = Cursors.Hand}
        AddHandler cancel.Click, Sub(sender, args) Me.DialogResult = False
        actions.Children.Add(cancel)
        Dim confirm As New Button With {.Content = "Aceptar y cobrar", .Height = 42, .Margin = New Thickness(5, 0, 0, 0), .Background = New SolidColorBrush(Color.FromRgb(&H34, &H78, &HD4)), .Foreground = Brushes.White, .BorderThickness = New Thickness(0), .FontWeight = FontWeights.SemiBold, .Cursor = Cursors.Hand}
        AddHandler confirm.Click, Sub(sender, args)
                                      If String.IsNullOrWhiteSpace(_customer.Text) Then _customer.Text = "Consumidor final"
                                      Me.DialogResult = True
                                  End Sub
        Grid.SetColumn(confirm, 1)
        actions.Children.Add(confirm)
        Grid.SetRow(actions, 4)
        root.Children.Add(actions)
        Return New Border With {.Background = Brushes.White, .CornerRadius = New CornerRadius(13), .Padding = New Thickness(1), .Child = root}
    End Function
End Class
