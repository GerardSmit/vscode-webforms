using System.Collections.Concurrent;
using WebForms.Models;

namespace WebForms.Services;

public class ProjectService
{
    private readonly HashSet<string> _validatedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Project> _projects = new();
    private bool _enableInspections;

    public Project? GetProject(string filePath)
    {
        var path = Path.GetDirectoryName(filePath);

        if (path == null)
        {
            return null;
        }

        foreach (var (key, project) in _projects)
        {
            if (path.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                return project;
            }
        }

        while (path != null)
        {
            var current = path;
            path = Directory.GetParent(path)?.FullName;

            if (!_validatedPaths.Add(current))
            {
                continue;
            }

            var binPath = Path.Combine(current, "bin");

            if (Directory.Exists(binPath))
            {
                return _projects.AddOrUpdate(current, CreateProject, (_, left) => left);
            }
        }

        return null;
    }

    public void ToggleInspections(bool value)
    {
        if (_enableInspections == value)
        {
            return;
        }

        _enableInspections = value;

        foreach (var project in _projects.Values)
        {
            if (value)
            {
                project.LoadAssemblies();
            }
            else
            {
                project.UnloadAssemblies();
            }
        }

        if (!value)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private Project CreateProject(string directory)
    {
        var project = new Project(directory);
        project.Load();

        if (_enableInspections)
        {
            project.LoadAssemblies();
        }

        return project;
    }
}
