Public Module StartupEntry
    Public Sub Main()
        Dim app As New System.Windows.Application()
        app.Run(New NewPOS.MainWindow())
    End Sub
End Module
