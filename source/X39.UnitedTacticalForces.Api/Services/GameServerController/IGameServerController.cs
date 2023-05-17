using System.Globalization;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.GameServerController;

/// <summary>
/// Interface for interacting with a <see cref="GameServer"/>.
/// </summary>
/// <remarks>
/// A controller should not implement this interface but instead use <see cref="IGameServerControllerCreatable"/> as
/// a base class.
/// </remarks>
public interface IGameServerController
{
    /// <summary>
    /// If <see langword="true"/>, the <see cref="IGameServerController"/> will apply all
    /// <see cref="ConfigurationEntry"/>'s passed into <see cref="UpdateConfigurationAsync"/>.
    /// </summary>
    bool AllowAnyConfigurationEntry { get; }

    /// <summary>
    /// Changes the configuration of the <see cref="IGameServerController"/>.
    /// </summary>
    /// <remarks>
    ///     <see cref="AllowAnyConfigurationEntry"/> may indicate that
    ///     the user is allowed to provide other configuration entries for <see cref="UpdateConfigurationAsync"/>
    ///     to allow for eg. customization of modifications.
    ///     However, given <see cref="AllowAnyConfigurationEntry"/> is <see langword="false"/>,
    ///     this is the complete list of actually applied <see cref="ConfigurationEntry"/>'s.
    /// </remarks>
    /// <param name="cultureInfo">
    ///     The locale to use for display <see cref="string"/>'s.
    /// </param>
    /// <returns>
    ///     A yieldable <see cref="IEnumerable{T}"/> which contains the definition information for the accepted
    ///     <see cref="ConfigurationEntry"/>.
    /// </returns>
    IEnumerable<ConfigurationEntryDefinition> GetConfigurationEntryDefinitions(
        CultureInfo cultureInfo);

    /// <summary>
    /// <see langword="bool"/> indicating whether the method <see cref="UpdateConfigurationAsync"/> may be called right now.
    /// </summary>
    bool CanUpdateConfiguration { get; }

    /// <summary>
    /// Updates the configuration of the <see cref="IGameServerController"/>.
    /// </summary>
    /// <remarks>
    ///     The method may not throw if configuration entries are provided which are not defined in
    ///     <see cref="GetConfigurationEntryDefinitions"/>, even if <see cref="AllowAnyConfigurationEntry"/> is
    ///     <see langword="false"/>.
    /// </remarks>
    /// <returns>
    ///     An awaitable <see cref="Task"/> which completes once the installation or upgrade was fully completed.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the method was called but <see cref="UpdateConfigurationAsync"/> was <see langword="false"/>.</exception>
    Task UpdateConfigurationAsync();

    /// <summary>
    /// <see langword="bool"/> indicating whether the method <see cref="StartAsync"/> may be called right now.
    /// </summary>
    bool CanStart { get; }

    /// <summary>
    /// Stops a started <see cref="GameServer"/> instance.
    /// </summary>
    /// <remarks>
    /// Check <see cref="CanStart"/> prior to calling to make sure that starting is possible.
    /// </remarks>
    /// <param name="executingUser">
    ///     The user that executed the shutdown or <see langword="null"/> if the system initiated the change.
    /// </param>
    /// <returns>
    ///     An awaitable <see cref="Task"/> which completes once the installation or upgrade was fully completed.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the method was called but <see cref="CanStart"/> was <see langword="false"/>.</exception>
    Task StartAsync(User? executingUser);

    /// <summary>
    /// <see langword="bool"/> indicating whether the method <see cref="StopAsync"/> may be called right now.
    /// </summary>
    bool CanStop { get; }

    /// <summary>
    /// Stops a started <see cref="GameServer"/> instance.
    /// </summary>
    /// <param name="executingUser">
    ///     The user that executed the shutdown or <see langword="null"/> if the system initiated the change.
    /// </param>
    /// <remarks>
    /// Check <see cref="CanStop"/> prior to calling to make sure that stopping is possible.
    /// </remarks>
    /// <returns>
    ///     An awaitable <see cref="Task"/> which completes once the installation or upgrade was fully completed.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the method was called but <see cref="CanStop"/> was <see langword="false"/>.</exception>
    Task StopAsync(User? executingUser);

    /// <summary>
    /// <see langword="bool"/> indicating whether the method <see cref="InstallOrUpgradeAsync"/> may be called right now.
    /// </summary>
    bool CanInstallOrUpgrade { get; }

    /// <summary>
    /// Whether the controller is hosting a physical instance of the <see cref="GameServer"/> that is active.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Installs or upgrades a <see cref="GameServer"/> instance and any possibly linked mods.
    /// </summary>
    /// <remarks>
    /// Check <see cref="CanInstallOrUpgrade"/> prior to calling to make sure that installing or upgrading is possible.
    /// </remarks>
    /// <param name="executingUser">
    ///     The user that executed the shutdown or <see langword="null"/> if the system initiated the change.
    /// </param>
    /// <returns>
    ///     An awaitable <see cref="Task"/> which completes once the installation or upgrade was fully completed.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the method was called but <see cref="CanInstallOrUpgrade"/> was <see langword="false"/>.</exception>
    Task InstallOrUpgradeAsync(User? executingUser);

    /// <summary>
    /// <see langword="bool"/> indicating whether the method <see cref="UploadFileAsync"/>
    /// or <see cref="DeleteFileAsync"/> may be called right now.
    /// </summary>
    bool CanModifyGameFiles { get; }

    /// <summary>
    /// Returns existing or virtual game folders, users can upload files to.
    /// </summary>
    /// <param name="cultureInfo">The locale to use for display <see cref="string"/>'s.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>The available game folders.</returns>
    Task<IEnumerable<GameFolder>> GetGameFoldersAsync(
        CultureInfo cultureInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns existing or virtual game folders, users can upload files to.
    /// </summary>
    /// <param name="cultureInfo">The locale to use for display <see cref="string"/>'s.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <param name="folder">The folder to get files from.</param>
    /// <returns>The available files in a <see cref="GameFolder"/>.</returns>
    Task<IEnumerable<GameFileInfo>> GetGameFolderFilesAsync(
        GameFolder folder,
        CultureInfo cultureInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the data of an existing or a virtual game file.
    /// </summary>
    /// <remarks>
    /// Caller is responsible of disposing the returned <see cref="Stream"/>.
    /// </remarks>
    /// <param name="folder">The folder to get a file from.</param>
    /// <param name="file">The file to get.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>An awaitable <see cref="Task"/> which completes once the file was uploaded and contains the files stream.</returns>
    Task<Stream> GetGameFolderFileAsync(
        GameFolder folder,
        GameFileInfo file,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a file to a game folder.
    /// </summary>
    /// <remarks>
    /// May also be used to update an existing file.
    /// </remarks>
    /// <param name="folder">The folder to upload a file into.</param>
    /// <param name="file">Information about the file.</param>
    /// <param name="stream">The data to upload.</param>
    /// <returns>An awaitable <see cref="Task"/> which completes once the file was uploaded.</returns>
    Task UploadFileAsync(GameFolder folder, GameFileInfo file, Stream stream);

    /// <summary>
    /// Deletes a file from a game folder.
    /// </summary>
    /// <param name="folder">The folder to upload a file into.</param>
    /// <param name="file">Information about the file.</param>
    /// <returns>An awaitable <see cref="Task"/> which completes once the file was uploaded.</returns>
    Task DeleteFileAsync(GameFolder folder, GameFileInfo file);
}