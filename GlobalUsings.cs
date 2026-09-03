// Projetos WPF nao incluem System.IO nos implicit usings (evita ambiguidade com
// System.Windows.Shapes.Path). Como nenhuma View usa Shapes.Path, importamos aqui.
global using System.IO;
