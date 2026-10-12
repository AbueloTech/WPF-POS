Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text

Public NotInheritable Class SqliteDatabase
    Private Const SQLITE_OK As Integer = 0
    Private Const SQLITE_ROW As Integer = 100
    Private Const SQLITE_DONE As Integer = 101
    Private Const SQLITE_TRANSIENT As Integer = -1
    Private ReadOnly _databasePath As String

    Public Sub New(databasePath As String)
        _databasePath = databasePath
    End Sub

    Public Function GetRows(sql As String, ParamArray values As Object()) As List(Of String())
        Dim database As IntPtr = IntPtr.Zero
        Dim statement As IntPtr = IntPtr.Zero
        Dim rows As New List(Of String())()
        Try
            OpenDatabase(database)
            Prepare(database, sql, statement)
            For index = 0 To values.Length - 1
                BindValue(statement, index + 1, values(index))
            Next
            Dim result = sqlite3_step(statement)
            While result = SQLITE_ROW
                Dim count = sqlite3_column_count(statement)
                Dim row(count - 1) As String
                For column = 0 To count - 1
                    Dim value = sqlite3_column_text16(statement, column)
                    row(column) = If(value = IntPtr.Zero, "", Marshal.PtrToStringUni(value))
                Next
                rows.Add(row)
                result = sqlite3_step(statement)
            End While
            If result <> SQLITE_DONE Then Throw New InvalidOperationException(GetError(database))
            Return rows
        Finally
            If statement <> IntPtr.Zero Then sqlite3_finalize(statement)
            If database <> IntPtr.Zero Then sqlite3_close_v2(database)
        End Try
    End Function

    Public Function Execute(sql As String, ParamArray values As Object()) As Integer
        Dim database As IntPtr = IntPtr.Zero
        Dim statement As IntPtr = IntPtr.Zero
        Try
            OpenDatabase(database)
            Prepare(database, sql, statement)
            For index = 0 To values.Length - 1
                BindValue(statement, index + 1, values(index))
            Next
            Dim result = sqlite3_step(statement)
            If result <> SQLITE_DONE Then Throw New InvalidOperationException(GetError(database))
            Return sqlite3_changes(database)
        Finally
            If statement <> IntPtr.Zero Then sqlite3_finalize(statement)
            If database <> IntPtr.Zero Then sqlite3_close_v2(database)
        End Try
    End Function

    Public Function SavePosSale(customer As String, paymentMethod As String, items As IList(Of PosCartItem)) As Long
        Dim database As IntPtr = IntPtr.Zero
        Try
            OpenDatabase(database)
            ExecuteOnConnection(database, "BEGIN IMMEDIATE")
            Try
                Dim total = items.Sum(Function(item) item.Subtotal)
                Dim saleNumber = ReadInt64OnConnection(database, "SELECT COALESCE(MAX(CAST(NTicket AS INTEGER)), 0) + 1 FROM Ventas")
                Dim saleDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Globalization.CultureInfo.InvariantCulture)
                ExecuteOnConnection(database, "INSERT INTO Ventas (Fecha, NTicket, Cliente, Monto, FormaPago) VALUES (?, ?, ?, ?, ?)", saleDate, saleNumber, customer, total, paymentMethod)
                For Each item In items
                    ExecuteOnConnection(database, "INSERT INTO Detalles (Fecha, NTicket, Plato, Precio, Cantidad, Categoria, Importe) VALUES (?, ?, ?, ?, ?, ?, ?)", saleDate, saleNumber, item.Producto.Nombre, item.Producto.Precio, item.Cantidad, item.Producto.Categoria, item.Subtotal)
                Next
                ExecuteOnConnection(database, "COMMIT")
                Return saleNumber
            Catch
                Try
                    ExecuteOnConnection(database, "ROLLBACK")
                Catch
                End Try
                Throw
            End Try
        Finally
            If database <> IntPtr.Zero Then sqlite3_close_v2(database)
        End Try
    End Function

    Private Shared Function ReadInt64OnConnection(database As IntPtr, sql As String) As Long
        Dim statement As IntPtr = IntPtr.Zero
        Try
            Prepare(database, sql, statement)
            If sqlite3_step(statement) <> SQLITE_ROW Then Throw New InvalidOperationException(GetError(database))
            Return sqlite3_column_int64(statement, 0)
        Finally
            If statement <> IntPtr.Zero Then sqlite3_finalize(statement)
        End Try
    End Function

    Private Shared Sub ExecuteOnConnection(database As IntPtr, sql As String, ParamArray values As Object())
        Dim statement As IntPtr = IntPtr.Zero
        Try
            Prepare(database, sql, statement)
            For index = 0 To values.Length - 1
                BindValue(statement, index + 1, values(index))
            Next
            Dim result = sqlite3_step(statement)
            If result <> SQLITE_DONE Then Throw New InvalidOperationException(GetError(database))
        Finally
            If statement <> IntPtr.Zero Then sqlite3_finalize(statement)
        End Try
    End Sub

    Private Sub OpenDatabase(ByRef database As IntPtr)
        Dim result = sqlite3_open16(_databasePath, database)
        If result <> SQLITE_OK Then Throw New InvalidOperationException(If(database = IntPtr.Zero, "No se pudo abrir la base de datos.", GetError(database)))
    End Sub

    Private Shared Sub Prepare(database As IntPtr, sql As String, ByRef statement As IntPtr)
        Dim result = sqlite3_prepare16_v2(database, sql, -1, statement, IntPtr.Zero)
        If result <> SQLITE_OK Then Throw New InvalidOperationException(GetError(database))
    End Sub

    Private Shared Sub BindValue(statement As IntPtr, index As Integer, value As Object)
        Dim result As Integer
        If value Is Nothing Then
            result = sqlite3_bind_null(statement, index)
        ElseIf TypeOf value Is Double OrElse TypeOf value Is Single OrElse TypeOf value Is Decimal Then
            result = sqlite3_bind_double(statement, index, Convert.ToDouble(value, Globalization.CultureInfo.InvariantCulture))
        ElseIf TypeOf value Is Integer OrElse TypeOf value Is Long Then
            result = sqlite3_bind_int64(statement, index, Convert.ToInt64(value, Globalization.CultureInfo.InvariantCulture))
        Else
            result = sqlite3_bind_text16(statement, index, Convert.ToString(value, Globalization.CultureInfo.InvariantCulture), -1, New IntPtr(SQLITE_TRANSIENT))
        End If
        If result <> SQLITE_OK Then Throw New InvalidOperationException("No se pudo asignar un parámetro SQLite.")
    End Sub

    Private Shared Function GetError(database As IntPtr) As String
        Dim message = sqlite3_errmsg16(database)
        Return If(message = IntPtr.Zero, "Error desconocido de SQLite.", Marshal.PtrToStringUni(message))
    End Function

    <DllImport("winsqlite3.dll", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Unicode, ExactSpelling:=True)>
    Private Shared Function sqlite3_open16(filename As String, ByRef database As IntPtr) As Integer
    End Function

    <DllImport("winsqlite3.dll", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Unicode, ExactSpelling:=True)>
    Private Shared Function sqlite3_prepare16_v2(database As IntPtr, sql As String, byteCount As Integer, ByRef statement As IntPtr, tail As IntPtr) As Integer
    End Function

    <DllImport("winsqlite3.dll", CallingConvention:=CallingConvention.Cdecl, ExactSpelling:=True)>
    Private Shared Function sqlite3_step(statement As IntPtr) As Integer
    End Function

    <DllImport("winsqlite3.dll", CallingConvention:=CallingConvention.Cdecl, ExactSpelling:=True)>
    Private Shared Function sqlite3_finalize(statement As IntPtr) As Integer
    End Function

    <DllImport("winsqlite3.dll", CallingConvention:=CallingConvention.Cdecl, ExactSpelling:=True)>
    Private Shared Function sqlite3_close_v2(database As IntPtr) As Integer
    End Function

    <DllImport("winsqlite3.dll", CallingConvention:=CallingConvention.Cdecl, ExactSpelling:=True)>
    Private Shared Function sqlite3_column_count(statement As IntPtr) As Integer
    End Function

    <DllImport("winsqlite3.dll", CallingConvention:=CallingConvention.Cdecl, ExactSpelling:=True)>
    Private Shared Function sqlite3_column_text16(statement As IntPtr, column As Integer) As IntPtr
    End Function

    <DllImport("winsqlite3.dll", CallingConvention:=CallingConvention.Cdecl, ExactSpelling:=True)>
    Private Shared Function sqlite3_changes(database As IntPtr) As Integer
    End Function

    <DllImport("winsqlite3.dll", CallingConvention:=CallingConvention.Cdecl, ExactSpelling:=True)>
    Private Shared Function sqlite3_column_int64(statement As IntPtr, column As Integer) As Long
    End Function

    <DllImport("winsqlite3.dll", CallingConvention:=CallingConvention.Cdecl, ExactSpelling:=True)>
    Private Shared Function sqlite3_errmsg16(database As IntPtr) As IntPtr
    End Function

    <DllImport("winsqlite3.dll", CallingConvention:=CallingConvention.Cdecl, ExactSpelling:=True)>
    Private Shared Function sqlite3_bind_null(statement As IntPtr, index As Integer) As Integer
    End Function

    <DllImport("winsqlite3.dll", CallingConvention:=CallingConvention.Cdecl, ExactSpelling:=True)>
    Private Shared Function sqlite3_bind_double(statement As IntPtr, index As Integer, value As Double) As Integer
    End Function

    <DllImport("winsqlite3.dll", CallingConvention:=CallingConvention.Cdecl, ExactSpelling:=True)>
    Private Shared Function sqlite3_bind_int64(statement As IntPtr, index As Integer, value As Long) As Integer
    End Function

    <DllImport("winsqlite3.dll", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Unicode, ExactSpelling:=True)>
    Private Shared Function sqlite3_bind_text16(statement As IntPtr, index As Integer, value As String, byteCount As Integer, destructor As IntPtr) As Integer
    End Function
End Class

Public Class PlatoRecord
    Public Property Id As Integer
    Public Property Nombre As String
    Public Property Precio As Double
    Public Property Categoria As String
    Public Property Imagen As String
End Class
