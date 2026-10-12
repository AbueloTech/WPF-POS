Imports System.Globalization
Imports System.IO
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports Microsoft.Win32

Public Class ProductosPage
    Inherits Page

    Private ReadOnly _database As SqliteDatabase
    Private ReadOnly _categoryButtons As StackPanel
    Private ReadOnly _productsGrid As UniformGrid
    Private ReadOnly _status As TextBlock
    Private ReadOnly _nameInput As TextBox
    Private ReadOnly _priceInput As TextBox
    Private ReadOnly _categoryInput As ComboBox
    Private ReadOnly _imageInput As TextBox
    Private ReadOnly _imagePreview As Image
    Private ReadOnly _imagePreviewFallback As TextBlock
    Private _updateButton As Button
    Private _deleteButton As Button
    Private _selectedId As Integer?
    Private _activeCategory As String = "Todas"
    Private _products As New List(Of PlatoRecord)()

    Public Sub New()
        Background = Brush("#F3F6FA")
        _database = New SqliteDatabase(LocateProjectFile("TerrenoBroaster.db"))
        _categoryButtons = New StackPanel()
        _productsGrid = New UniformGrid With {.Columns = 4}
        _status = New TextBlock With {.Foreground = Brush("#718096"), .FontSize = 11, .VerticalAlignment = VerticalAlignment.Center, .TextWrapping = TextWrapping.Wrap}
        _nameInput = New TextBox()
        _priceInput = New TextBox()
        _categoryInput = New ComboBox With {.IsEditable = True, .IsTextSearchEnabled = False, .Height = 36}
        _imageInput = New TextBox()
        _imagePreview = New Image With {.Stretch = Stretch.UniformToFill, .ClipToBounds = True}
        _imagePreviewFallback = New TextBlock With {.Text = "▧  Vista previa de imagen", .Foreground = Brush("#8996A8"), .FontSize = 10, .HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Center}
        _updateButton = New Button()
        _deleteButton = New Button()
        Content = BuildLayout()
        LoadData()
    End Sub

    Private Function BuildLayout() As UIElement
        Dim root As New Grid With {.Margin = New Thickness(22, 18, 22, 18)}
        root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        root.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})

        Dim header As New Grid With {.Margin = New Thickness(0, 0, 0, 15)}
        header.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        header.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim heading As New StackPanel()
        heading.Children.Add(New TextBlock With {.Text = "PRODUCTOS", .Foreground = Brush("#3478D4"), .FontSize = 10, .FontWeight = FontWeights.SemiBold})
        heading.Children.Add(New TextBlock With {.Text = "Platos", .Foreground = Brush("#172B4D"), .FontSize = 27, .FontWeight = FontWeights.SemiBold, .Margin = New Thickness(0, 3, 0, 0)})
        heading.Children.Add(New TextBlock With {.Text = "Catálogo de productos · Terreno Broaster", .Foreground = Brush("#718096"), .FontSize = 12, .Margin = New Thickness(0, 3, 0, 0)})
        header.Children.Add(heading)
        Dim countLabel As New Border With {.Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .CornerRadius = New CornerRadius(9), .Padding = New Thickness(13, 8, 13, 8), .VerticalAlignment = VerticalAlignment.Center}
        countLabel.Child = _status
        Grid.SetColumn(countLabel, 1)
        header.Children.Add(countLabel)
        root.Children.Add(header)

        Dim body As New Grid()
        body.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(155)})
        body.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star), .MinWidth = 360})
        body.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(270)})
        Grid.SetRow(body, 1)
        root.Children.Add(body)

        Dim categoryBorder As New Border With {.Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .CornerRadius = New CornerRadius(12), .Margin = New Thickness(0, 0, 12, 0), .Padding = New Thickness(10)}
        Dim categoryStack As New DockPanel()
        Dim categoryTitle As New TextBlock With {.Text = "CATEGORÍAS", .Foreground = Brush("#718096"), .FontSize = 9, .FontWeight = FontWeights.Bold, .Margin = New Thickness(7, 5, 4, 10)}
        DockPanel.SetDock(categoryTitle, Dock.Top)
        categoryStack.Children.Add(categoryTitle)
        Dim categoryFrame As New Frame With {.NavigationUIVisibility = NavigationUIVisibility.Hidden, .Background = Brushes.Transparent, .BorderThickness = New Thickness(0), .Content = New Page With {.Background = Brushes.Transparent, .Content = _categoryButtons}}
        categoryStack.Children.Add(categoryFrame)
        categoryBorder.Child = categoryStack
        body.Children.Add(categoryBorder)

        Dim productsBorder As New Border With {.Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .CornerRadius = New CornerRadius(12), .Margin = New Thickness(0, 0, 12, 0), .Padding = New Thickness(7)}
        Dim productsFrame As New Frame With {.Name = "ProductsFrame", .NavigationUIVisibility = NavigationUIVisibility.Hidden, .Background = Brushes.Transparent, .BorderThickness = New Thickness(0)}
        Dim productScroll As New ScrollViewer With {.VerticalScrollBarVisibility = ScrollBarVisibility.Auto, .HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, .Padding = New Thickness(2)}
        productScroll.Content = _productsGrid
        productsFrame.Content = New Page With {.Background = Brushes.Transparent, .Content = productScroll}
        productsBorder.Child = productsFrame
        Grid.SetColumn(productsBorder, 1)
        body.Children.Add(productsBorder)

        Dim editor = BuildEditor()
        Grid.SetColumn(editor, 2)
        body.Children.Add(editor)
        Return root
    End Function

    Private Function BuildEditor() As UIElement
        Dim panel As New Border With {.Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .CornerRadius = New CornerRadius(12), .Padding = New Thickness(17)}
        Dim content As New DockPanel()
        Dim title As New TextBlock With {.Text = "DETALLE DEL PLATO", .Foreground = Brush("#718096"), .FontSize = 9, .FontWeight = FontWeights.Bold, .Margin = New Thickness(0, 2, 0, 16)}
        DockPanel.SetDock(title, Dock.Top)
        content.Children.Add(title)
        Dim fields As New StackPanel()
        fields.Children.Add(CreatePlaceholderInput(_nameInput, "Nombre del plato"))
        _nameInput.Height = 34
        fields.Children.Add(CreatePlaceholderInput(_priceInput, "Precio"))
        _priceInput.Height = 34
        _priceInput.Margin = New Thickness(0, 0, 0, 10)
        _priceInput.ToolTip = "Precio en pesos colombianos"
        _categoryInput.Margin = New Thickness(0, 0, 0, 13)
        fields.Children.Add(CreatePlaceholderInput(_categoryInput, "Categoría"))
        Dim imageRow As New Grid With {.Margin = New Thickness(0, 0, 0, 13)}
        imageRow.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        imageRow.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        _imageInput.Height = 34
        _imageInput.VerticalContentAlignment = VerticalAlignment.Center
        imageRow.Children.Add(CreatePlaceholderInput(_imageInput, "Archivo de imagen"))
        Dim browse As Button = MakeButton("…", "#EDF3FA", "#344B6E")
        browse.Width = 38
        browse.Height = 36
        browse.Margin = New Thickness(6, 0, 0, 0)
        browse.ToolTip = "Seleccionar y copiar una imagen a la carpeta img"
        AddHandler browse.Click, AddressOf BrowseImage_Click
        Grid.SetColumn(browse, 1)
        imageRow.Children.Add(browse)
        fields.Children.Add(imageRow)

        Dim previewBorder As New Border With {.Height = 118, .Margin = New Thickness(0, 0, 0, 12), .Background = Brush("#F5F8FC"), .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .CornerRadius = New CornerRadius(9), .ClipToBounds = True}
        Dim previewGrid As New Grid()
        previewGrid.Children.Add(_imagePreviewFallback)
        previewGrid.Children.Add(_imagePreview)
        previewBorder.Child = previewGrid
        fields.Children.Add(previewBorder)

        Dim buttons As New UniformGrid With {.Columns = 2, .Margin = New Thickness(0, 4, 0, 0)}
        Dim newButton = MakeButton("＋  Nuevo", "#EEF2F8", "#344B6E")
        AddHandler newButton.Click, AddressOf New_Click
        buttons.Children.Add(newButton)
        Dim saveButton = MakeButton("Guardar", "#3478D4", "White")
        AddHandler saveButton.Click, AddressOf Save_Click
        buttons.Children.Add(saveButton)
        _updateButton = MakeButton("Actualizar", "#DCEBFF", "#245EA8")
        AddHandler _updateButton.Click, AddressOf Update_Click
        buttons.Children.Add(_updateButton)
        _deleteButton = MakeButton("Eliminar", "#FDECEC", "#B33E3E")
        AddHandler _deleteButton.Click, AddressOf Delete_Click
        buttons.Children.Add(_deleteButton)
        fields.Children.Add(buttons)

        Dim hint As New TextBlock With {.Text = "Selecciona una tarjeta para cargar sus datos y actualizarla o eliminarla.", .Foreground = Brush("#8996A8"), .FontSize = 10, .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(1, 13, 0, 0)}
        fields.Children.Add(hint)
        content.Children.Add(fields)
        panel.Child = content
        SetEditing(False)
        Return panel
    End Function

    Private Sub LoadData()
        Try
            _products = ReadProducts()
            RebuildCategoryButtons()
            RebuildCategoryChoices()
            RenderProducts()
            SetStatus(_products.Count.ToString() & " productos", False)
        Catch ex As Exception
            SetStatus("No se pudo leer TerrenoBroaster.db: " & ex.Message, True)
            MessageBox.Show("No fue posible cargar el catálogo." & Environment.NewLine & ex.Message, "NewPOS · Productos", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Function ReadProducts() As List(Of PlatoRecord)
        Dim result As New List(Of PlatoRecord)()
        Dim sql = "SELECT ID, Plato, Precio, Categoria, Imagen FROM Platos ORDER BY Plato COLLATE NOCASE"
        For Each values In _database.GetRows(sql)
            result.Add(New PlatoRecord With {
                .Id = Integer.Parse(values(0), CultureInfo.InvariantCulture),
                .Nombre = values(1),
                .Precio = Double.Parse(values(2), NumberStyles.Any, CultureInfo.InvariantCulture),
                .Categoria = values(3),
                .Imagen = values(4)
            })
        Next
        Return result
    End Function

    Private Sub RebuildCategoryButtons()
        _categoryButtons.Children.Clear()
        AddCategoryButton("Todas", "▦", _products.Count)
        For Each category In _products.Select(Function(item) item.Categoria).Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(Function(value) value)
            AddCategoryButton(category, CategoryGlyph(category), _products.Where(Function(item) String.Equals(item.Categoria, category, StringComparison.CurrentCultureIgnoreCase)).Count())
        Next
    End Sub

    Private Sub AddCategoryButton(category As String, glyph As String, count As Integer)
        Dim active = String.Equals(_activeCategory, category, StringComparison.CurrentCultureIgnoreCase)
        Dim button = MakeButton(glyph & "   " & category & "  " & count.ToString(), If(active, "#EAF2FC", "Transparent"), If(active, "#2F66A5", "#52627A"))
        button.HorizontalContentAlignment = HorizontalAlignment.Left
        button.Margin = New Thickness(0, 2, 0, 2)
        button.Padding = New Thickness(9, 9, 5, 9)
        button.FontSize = 11
        button.Tag = category
        AddHandler button.Click, AddressOf Category_Click
        _categoryButtons.Children.Add(button)
    End Sub

    Private Sub RebuildCategoryChoices()
        Dim current = _categoryInput.Text
        _categoryInput.Items.Clear()
        For Each category In _products.Select(Function(item) item.Categoria).Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(Function(value) value)
            _categoryInput.Items.Add(category)
        Next
        _categoryInput.Text = current
    End Sub

    Private Sub RenderProducts()
        _productsGrid.Children.Clear()
        Dim visibleProducts = If(_activeCategory = "Todas", _products, _products.Where(Function(item) String.Equals(item.Categoria, _activeCategory, StringComparison.CurrentCultureIgnoreCase)).ToList())
        For Each product In visibleProducts
            _productsGrid.Children.Add(BuildProductCard(product))
        Next
        If visibleProducts.Count = 0 Then
            _productsGrid.Children.Add(New TextBlock With {.Text = "No hay platos en esta categoría.", .Foreground = Brush("#718096"), .Margin = New Thickness(20), .FontSize = 13})
        End If
    End Sub

    Private Function BuildProductCard(product As PlatoRecord) As UIElement
        Dim button As New Button With {.Tag = product.Id, .Padding = New Thickness(0), .Margin = New Thickness(6), .Background = Brushes.White, .BorderBrush = Brush("#E4EAF1"), .BorderThickness = New Thickness(1), .Cursor = Cursors.Hand, .ToolTip = product.Nombre}
        button.Template = CardButtonTemplate()
        Dim stack As New StackPanel()
        stack.Children.Add(ProductImage(product.Imagen))
        Dim textPanel As New StackPanel With {.Margin = New Thickness(10, 7, 10, 10)}
        textPanel.Children.Add(New TextBlock With {.Text = product.Nombre, .Foreground = Brush("#263750"), .FontSize = 11, .FontWeight = FontWeights.SemiBold, .TextTrimming = TextTrimming.CharacterEllipsis, .HorizontalAlignment = HorizontalAlignment.Left})
        textPanel.Children.Add(New TextBlock With {.Text = product.Precio.ToString("C0", CultureInfo.GetCultureInfo("es-CO")), .Foreground = Brush("#3478D4"), .FontSize = 12, .FontWeight = FontWeights.Bold, .Margin = New Thickness(0, 4, 0, 0)})
        stack.Children.Add(textPanel)
        button.Content = stack
        AddHandler button.Click, AddressOf Product_Click
        Return button
    End Function

    Private Function ProductImage(fileName As String) As UIElement
        Dim imagePath = System.IO.Path.Combine(LocateProjectFile("img"), If(fileName, ""))
        If Not String.IsNullOrWhiteSpace(fileName) AndAlso File.Exists(imagePath) Then
            Try
                Dim bitmap As New BitmapImage()
                bitmap.BeginInit()
                bitmap.CacheOption = BitmapCacheOption.OnLoad
                bitmap.UriSource = New Uri(imagePath, UriKind.Absolute)
                bitmap.EndInit()
                Return New Image With {.Source = bitmap, .Height = 105, .Stretch = Stretch.UniformToFill}
            Catch
            End Try
        End If
        Dim placeholder As New Border With {.Height = 105, .Background = Brush("#EFF3F8"), .Child = New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}}
        Dim placeholderContent = DirectCast(placeholder.Child, StackPanel)
        placeholderContent.Children.Add(New TextBlock With {.Text = "▧", .Foreground = Brush("#AAB6C6"), .FontSize = 23, .HorizontalAlignment = HorizontalAlignment.Center})
        placeholderContent.Children.Add(New TextBlock With {.Text = "Sin imagen", .Foreground = Brush("#8996A8"), .FontSize = 9, .HorizontalAlignment = HorizontalAlignment.Center})
        Return placeholder
    End Function

    Private Sub Category_Click(sender As Object, e As RoutedEventArgs)
        _activeCategory = CStr(DirectCast(sender, Button).Tag)
        RebuildCategoryButtons()
        RenderProducts()
    End Sub

    Private Sub Product_Click(sender As Object, e As RoutedEventArgs)
        Dim id = CInt(DirectCast(sender, Button).Tag)
        Dim product = _products.FirstOrDefault(Function(item) item.Id = id)
        If product Is Nothing Then Return
        _selectedId = product.Id
        _nameInput.Text = product.Nombre
        _priceInput.Text = product.Precio.ToString("0.##", CultureInfo.InvariantCulture)
        _categoryInput.Text = product.Categoria
        _imageInput.Text = product.Imagen
        UpdateImagePreview(product.Imagen)
        SetEditing(True)
        SetStatus("Editando: " & product.Nombre, False)
    End Sub

    Private Sub New_Click(sender As Object, e As RoutedEventArgs)
        ClearEditor()
        SetStatus("Nuevo plato", False)
        _nameInput.Focus()
    End Sub

    Private Sub Save_Click(sender As Object, e As RoutedEventArgs)
        Dim values = ValidateEditor()
        If values Is Nothing Then Return
        Try
            _database.Execute("INSERT INTO Platos (Plato, Precio, Categoria, Imagen) VALUES (?, ?, ?, ?)", values.Nombre, values.Precio, values.Categoria, values.Imagen)
            RefreshAfterChange()
            ClearEditor()
            SetStatus("Plato agregado", False)
        Catch ex As Exception
            ShowDatabaseError("guardar el plato", ex)
        End Try
    End Sub

    Private Sub Update_Click(sender As Object, e As RoutedEventArgs)
        If Not _selectedId.HasValue Then Return
        Dim values = ValidateEditor()
        If values Is Nothing Then Return
        Try
            _database.Execute("UPDATE Platos SET Plato = ?, Precio = ?, Categoria = ?, Imagen = ? WHERE ID = ?", values.Nombre, values.Precio, values.Categoria, values.Imagen, _selectedId.Value)
            RefreshAfterChange()
            ClearEditor()
            SetStatus("Plato actualizado", False)
        Catch ex As Exception
            ShowDatabaseError("actualizar el plato", ex)
        End Try
    End Sub

    Private Sub Delete_Click(sender As Object, e As RoutedEventArgs)
        If Not _selectedId.HasValue Then Return
        Dim answer = MessageBox.Show("¿Eliminar el plato '" & _nameInput.Text.Trim() & "'?", "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning)
        If answer <> MessageBoxResult.Yes Then Return
        Try
            _database.Execute("DELETE FROM Platos WHERE ID = ?", _selectedId.Value)
            RefreshAfterChange()
            ClearEditor()
            SetStatus("Plato eliminado", False)
        Catch ex As Exception
            ShowDatabaseError("eliminar el plato", ex)
        End Try
    End Sub

    Private Sub BrowseImage_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog As New OpenFileDialog With {.Title = "Seleccionar imagen del plato", .Filter = "Archivos de imagen|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos los archivos|*.*", .CheckFileExists = True}
        If dialog.ShowDialog() <> True Then Return
        Try
            Dim imageDirectory = LocateProjectFile("img")
            Directory.CreateDirectory(imageDirectory)
            Dim destination = Path.Combine(imageDirectory, Path.GetFileName(dialog.FileName))
            If Not String.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase) Then File.Copy(dialog.FileName, destination, True)
            _imageInput.Text = Path.GetFileName(dialog.FileName)
            UpdateImagePreview(_imageInput.Text)
            SetStatus("Imagen lista: " & _imageInput.Text, False)
        Catch ex As Exception
            MessageBox.Show("No se pudo copiar la imagen a la carpeta img." & Environment.NewLine & ex.Message, "NewPOS · Productos", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Function CreatePlaceholderInput(input As TextBox, placeholder As String) As UIElement
        input.BorderThickness = New Thickness(0)
        input.Background = Brushes.Transparent
        input.Foreground = Brush("#263750")
        input.Padding = New Thickness(2, 7, 2, 3)
        input.VerticalContentAlignment = VerticalAlignment.Center
        Dim wrapper As New Grid With {.Margin = New Thickness(0, 0, 0, 10)}
        wrapper.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Dim placeholderText As New TextBlock With {.Text = placeholder, .Foreground = Brush("#8996A8"), .FontSize = 11, .Margin = New Thickness(3, 7, 0, 3), .IsHitTestVisible = False}
        Dim line As New Border With {.Height = 1, .Background = Brush("#CBD5E1"), .VerticalAlignment = VerticalAlignment.Bottom}
        wrapper.Children.Add(input)
        wrapper.Children.Add(placeholderText)
        wrapper.Children.Add(line)
        Dim update = Sub()
                         placeholderText.Visibility = If(String.IsNullOrWhiteSpace(input.Text), Visibility.Visible, Visibility.Collapsed)
                         line.Background = If(input.IsKeyboardFocusWithin, Brush("#3478D4"), Brush("#CBD5E1"))
                         line.Height = If(input.IsKeyboardFocusWithin, 2.0, 1.0)
                     End Sub
        AddHandler input.TextChanged, Sub(sender, args) update()
        AddHandler input.GotKeyboardFocus, Sub(sender, args) update()
        AddHandler input.LostKeyboardFocus, Sub(sender, args) update()
        AddHandler input.PreviewGotKeyboardFocus, Sub(sender, args) update()
        update()
        Return wrapper
    End Function

    Private Function CreatePlaceholderInput(input As ComboBox, placeholder As String) As UIElement
        input.Height = 34
        input.Background = Brushes.Transparent
        input.BorderThickness = New Thickness(0)
        input.Foreground = Brush("#263750")
        Dim wrapper As New Grid With {.Margin = New Thickness(0, 0, 0, 10)}
        Dim placeholderText As New TextBlock With {.Text = placeholder, .Foreground = Brush("#8996A8"), .FontSize = 11, .Margin = New Thickness(3, 8, 0, 0), .IsHitTestVisible = False}
        Dim line As New Border With {.Height = 1, .Background = Brush("#CBD5E1"), .VerticalAlignment = VerticalAlignment.Bottom}
        wrapper.Children.Add(input)
        wrapper.Children.Add(placeholderText)
        wrapper.Children.Add(line)
        Dim update = Sub()
                         placeholderText.Visibility = If(String.IsNullOrWhiteSpace(input.Text), Visibility.Visible, Visibility.Collapsed)
                         line.Background = If(input.IsKeyboardFocusWithin, Brush("#3478D4"), Brush("#CBD5E1"))
                         line.Height = If(input.IsKeyboardFocusWithin, 2.0, 1.0)
                     End Sub
        AddHandler input.SelectionChanged, Sub(sender, args) update()
        AddHandler input.KeyUp, Sub(sender, args) update()
        AddHandler input.GotKeyboardFocus, Sub(sender, args) update()
        AddHandler input.LostKeyboardFocus, Sub(sender, args) update()
        update()
        Return wrapper
    End Function

    Private Sub UpdateImagePreview(fileName As String)
        _imagePreview.Source = Nothing
        _imagePreviewFallback.Visibility = Visibility.Visible
        If String.IsNullOrWhiteSpace(fileName) Then Return
        Dim imagePath = Path.Combine(LocateProjectFile("img"), Path.GetFileName(fileName))
        If Not File.Exists(imagePath) Then Return
        Try
            Dim bitmap As New BitmapImage()
            bitmap.BeginInit()
            bitmap.CacheOption = BitmapCacheOption.OnLoad
            bitmap.UriSource = New Uri(imagePath, UriKind.Absolute)
            bitmap.EndInit()
            _imagePreview.Source = bitmap
            _imagePreviewFallback.Visibility = Visibility.Collapsed
        Catch
            _imagePreview.Source = Nothing
        End Try
    End Sub

    Private Function ValidateEditor() As PlatoRecord
        Dim name = _nameInput.Text.Trim()
        Dim category = _categoryInput.Text.Trim()
        Dim price As Double
        If name.Length = 0 Then
            MessageBox.Show("Escribe el nombre del plato.", "Validación", MessageBoxButton.OK, MessageBoxImage.Information)
            _nameInput.Focus()
            Return Nothing
        End If
        If Not Double.TryParse(_priceInput.Text.Trim(), NumberStyles.Number Or NumberStyles.AllowCurrencySymbol, CultureInfo.GetCultureInfo("es-CO"), price) AndAlso Not Double.TryParse(_priceInput.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, price) Then
            MessageBox.Show("Escribe un precio válido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Information)
            _priceInput.Focus()
            Return Nothing
        End If
        If price < 0 Then
            MessageBox.Show("El precio no puede ser negativo.", "Validación", MessageBoxButton.OK, MessageBoxImage.Information)
            _priceInput.Focus()
            Return Nothing
        End If
        If category.Length = 0 Then
            MessageBox.Show("Selecciona o escribe una categoría.", "Validación", MessageBoxButton.OK, MessageBoxImage.Information)
            _categoryInput.Focus()
            Return Nothing
        End If
        Return New PlatoRecord With {.Nombre = name, .Precio = price, .Categoria = category, .Imagen = Path.GetFileName(_imageInput.Text.Trim())}
    End Function

    Private Sub RefreshAfterChange()
        _products = ReadProducts()
        RebuildCategoryButtons()
        RebuildCategoryChoices()
        RenderProducts()
    End Sub

    Private Sub ClearEditor()
        _selectedId = Nothing
        _nameInput.Clear()
        _priceInput.Clear()
        _categoryInput.Text = ""
        _imageInput.Clear()
        UpdateImagePreview("")
        SetEditing(False)
    End Sub

    Private Sub SetEditing(editing As Boolean)
        _updateButton.IsEnabled = editing
        _deleteButton.IsEnabled = editing
        _updateButton.Opacity = If(editing, 1.0, 0.48)
        _deleteButton.Opacity = If(editing, 1.0, 0.48)
    End Sub

    Private Sub SetStatus(message As String, isError As Boolean)
        _status.Text = message
        _status.Foreground = If(isError, Brush("#B33E3E"), Brush("#718096"))
    End Sub

    Private Sub ShowDatabaseError(action As String, ex As Exception)
        SetStatus("Error al " & action, True)
        MessageBox.Show("No se pudo " & action & "." & Environment.NewLine & ex.Message, "NewPOS · Productos", MessageBoxButton.OK, MessageBoxImage.Error)
    End Sub

    Private Shared Function CategoryGlyph(category As String) As String
        Select Case category.ToLowerInvariant()
            Case "pollo" : Return "♨"
            Case "bebidas" : Return "◉"
            Case "extras" : Return "＋"
            Case "hamburguesas" : Return "▣"
            Case "pizzas" : Return "◉"
            Case Else : Return "•"
        End Select
    End Function

    Private Shared Function FieldLabel(label As String) As TextBlock
        Return New TextBlock With {.Text = label, .Foreground = Brush("#52627A"), .FontSize = 11, .FontWeight = FontWeights.SemiBold, .Margin = New Thickness(0, 0, 0, 6)}
    End Function

    Private Shared Function MakeButton(label As String, background As String, foreground As String) As Button
        Dim button As New Button With {.Content = label, .Background = Brush(background), .Foreground = Brush(foreground), .BorderThickness = New Thickness(0), .Padding = New Thickness(8, 8, 8, 8), .Margin = New Thickness(2), .FontSize = 11, .FontWeight = FontWeights.SemiBold, .Cursor = Cursors.Hand}
        button.Template = CardButtonTemplate(8)
        Return button
    End Function

    Private Shared Function CardButtonTemplate(Optional cornerRadius As Double = 10) As ControlTemplate
        Dim borderFactory As New FrameworkElementFactory(GetType(Border))
        borderFactory.SetValue(System.Windows.Controls.Border.BackgroundProperty, New TemplateBindingExtension(Control.BackgroundProperty))
        borderFactory.SetValue(System.Windows.Controls.Border.BorderBrushProperty, New TemplateBindingExtension(Control.BorderBrushProperty))
        borderFactory.SetValue(System.Windows.Controls.Border.BorderThicknessProperty, New TemplateBindingExtension(Control.BorderThicknessProperty))
        borderFactory.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, New CornerRadius(cornerRadius))
        Dim presenter As New FrameworkElementFactory(GetType(ContentPresenter))
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch)
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center)
        presenter.SetValue(ContentPresenter.MarginProperty, New TemplateBindingExtension(Control.PaddingProperty))
        borderFactory.AppendChild(presenter)
        Return New ControlTemplate(GetType(Button)) With {.VisualTree = borderFactory}
    End Function

    Private Shared Function Brush(color As String) As SolidColorBrush
        Return New SolidColorBrush(CType(ColorConverter.ConvertFromString(color), Color))
    End Function

    Private Shared Function LocateProjectFile(relativePath As String) As String
        Dim applicationDirectory = AppDomain.CurrentDomain.BaseDirectory
        Dim deployedPath = Path.Combine(applicationDirectory, relativePath)
        Dim projectPath = Path.GetFullPath(Path.Combine(applicationDirectory, "..", "..", relativePath))
        If File.Exists(deployedPath) OrElse Directory.Exists(deployedPath) Then Return deployedPath
        If File.Exists(projectPath) OrElse Directory.Exists(projectPath) Then Return projectPath
        Return deployedPath
    End Function
End Class
