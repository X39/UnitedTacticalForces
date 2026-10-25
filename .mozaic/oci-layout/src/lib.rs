//! UTF toolchain bootstrap adapted from the accepted Jodel registry verifier.
//! Verified Python content supports ordinary, unprivileged OCI assembly.

use std::collections::{BTreeMap};
use std::fs::{self, File, OpenOptions};
use std::io::{Read, Write};
use std::path::{Component, Path, PathBuf};
use std::time::Duration;

use flate2::read::GzDecoder;
use reqwest::blocking::{Client, Response};
use reqwest::header::{ACCEPT, AUTHORIZATION, LOCATION};
use reqwest::{StatusCode, Url};
use serde::{Deserialize, Serialize};
use serde_json::Value;
use sha2::{Digest, Sha256};

pub type Result<T> = std::result::Result<T, Box<dyn std::error::Error>>;
const MANIFEST: &str = "application/vnd.oci.image.manifest.v1+json";
const INDEX: &str = "application/vnd.oci.image.index.v1+json";
const CONFIG: &str = "application/vnd.oci.image.config.v1+json";
const LAYER: &str = "application/vnd.oci.image.layer.v1.tar";
const GZIP_LAYER: &str = "application/vnd.oci.image.layer.v1.tar+gzip";
const METADATA_LIMIT: u64 = 1024 * 1024;
const BLOB_LIMIT: u64 = 1024 * 1024 * 1024;
const EXPANDED_LIMIT: u64 = 1024 * 1024 * 1024;
const PUBLIC_HOSTS: &[&str] = &["registry-1.docker.io", "production.cloudfront.docker.com"];

fn require(condition: bool, message: &'static str) -> Result<()> {
    if !condition {
        return Err(message.into());
    }
    Ok(())
}

fn digest(bytes: &[u8]) -> String {
    format!("sha256:{:x}", Sha256::digest(bytes))
}

fn digest_name(value: &str) -> Result<&str> {
    let name = value.strip_prefix("sha256:").ok_or("unsupported digest")?;
    require(
        name.len() == 64
            && name
                .bytes()
                .all(|byte| byte.is_ascii_hexdigit() && !byte.is_ascii_uppercase()),
        "invalid SHA-256 digest",
    )?;
    Ok(name)
}

#[derive(Clone, Debug, Deserialize, Serialize)]
struct Descriptor {
    #[serde(rename = "mediaType")]
    media_type: String,
    digest: String,
    size: u64,
}

impl Descriptor {
    fn bytes(media_type: &str, bytes: &[u8]) -> Self {
        Self {
            media_type: media_type.into(),
            digest: digest(bytes),
            size: bytes.len() as u64,
        }
    }

    fn validate(&self, maximum: u64) -> Result<()> {
        digest_name(&self.digest)?;
        require(
            self.size > 0 && self.size <= maximum,
            "descriptor exceeds its byte limit",
        )
    }
}

/// Copies at most the explicit limit. A mismatching descriptor never becomes a
/// usable blob; callers retain all partial files only in a temporary directory.
fn copy_checked(
    mut input: impl Read,
    mut output: impl Write,
    maximum: u64,
    expected: Option<&Descriptor>,
) -> Result<(String, u64)> {
    if let Some(expected) = expected {
        expected.validate(maximum)?;
    }
    let mut hash = Sha256::new();
    let mut total = 0_u64;
    let mut buffer = [0_u8; 64 * 1024];
    loop {
        let count = input
            .read(&mut buffer)
            .map_err(|_| "bounded input read failed")?;
        if count == 0 {
            break;
        }
        total = total
            .checked_add(count as u64)
            .ok_or("byte count overflow")?;
        require(total <= maximum, "input exceeds its byte limit")?;
        if let Some(expected) = expected {
            require(total <= expected.size, "blob exceeds descriptor size")?;
        }
        hash.update(&buffer[..count]);
        output.write_all(&buffer[..count])?;
    }
    let actual = format!("sha256:{hash:x}", hash = hash.finalize());
    if let Some(expected) = expected {
        require(total == expected.size, "blob size mismatch")?;
        require(actual == expected.digest, "blob digest mismatch")?;
    }
    Ok((actual, total))
}

trait Fetch {
    fn fetch(
        &self,
        kind: &str,
        hash: &str,
        path: &Path,
        maximum: u64,
        expected: Option<&Descriptor>,
    ) -> Result<()>;
}

struct Registry {
    client: Client,
    token: String,
    repository: &'static str,
}

fn public_url(url: &Url) -> Result<()> {
    require(
        url.scheme() == "https"
            && url.username().is_empty()
            && url.password().is_none()
            && url.port().is_none_or(|port| port == 443)
            && url.fragment().is_none()
            && PUBLIC_HOSTS.contains(&url.host_str().unwrap_or("")),
        "registry redirect destination is not allowed",
    )
}

impl Registry {
    fn new(repository: &'static str) -> Result<Self> {
        require(
            matches!(repository, "alpine" | "node" | "python"),
            "unsupported public repository",
        )?;
        let client = Client::builder()
            .redirect(reqwest::redirect::Policy::none())
            .connect_timeout(Duration::from_secs(20))
            .timeout(Duration::from_secs(300))
            .build()
            .map_err(|_| "TLS client initialization failed")?;
        // This fixed public pull token is never accepted from source, persisted,
        // printed, or forwarded to blob CDN destinations.
        let response = client
            .get(format!("https://auth.docker.io/token?service=registry.docker.io&scope=repository:library/{repository}:pull"))
            .send()
            .map_err(|_| "public registry token request failed")?;
        require(
            response.status() == StatusCode::OK,
            "public registry token request rejected",
        )?;
        let mut bytes = Vec::new();
        copy_checked(response, &mut bytes, 32 * 1024, None)?;
        let token: Value =
            serde_json::from_slice(&bytes).map_err(|_| "invalid public registry token response")?;
        let token = token
            .get("token")
            .and_then(Value::as_str)
            .filter(|token| !token.is_empty() && token.len() <= 16 * 1024)
            .ok_or("missing bounded public registry token")?
            .to_owned();
        Ok(Self {
            client,
            token,
            repository,
        })
    }

    fn response(&self, mut url: Url) -> Result<Response> {
        for _ in 0..=3 {
            public_url(&url)?;
            let mut request = self
                .client
                .get(url.clone())
                .header(ACCEPT, format!("{MANIFEST}, {INDEX}"));
            if url.host_str() == Some("registry-1.docker.io") {
                require(
                    url.path()
                        .starts_with(&format!("/v2/library/{}/", self.repository)),
                    "registry path escaped pinned repository",
                )?;
                request = request.header(AUTHORIZATION, format!("Bearer {}", self.token));
            }
            let response = request
                .send()
                .map_err(|_| "bounded registry request failed")?;
            if response.status().is_redirection() {
                let location = response
                    .headers()
                    .get(LOCATION)
                    .ok_or("registry redirect has no location")?
                    .to_str()
                    .map_err(|_| "registry redirect location is invalid")?;
                url = url
                    .join(location)
                    .map_err(|_| "registry redirect URL is invalid")?;
                public_url(&url)?;
            } else {
                require(
                    response.status() == StatusCode::OK,
                    "registry fetch rejected",
                )?;
                return Ok(response);
            }
        }
        Err("registry redirect limit exceeded".into())
    }
}

impl Fetch for Registry {
    fn fetch(
        &self,
        kind: &str,
        hash: &str,
        path: &Path,
        maximum: u64,
        expected: Option<&Descriptor>,
    ) -> Result<()> {
        digest_name(hash)?;
        require(
            matches!(kind, "manifests" | "blobs"),
            "invalid registry resource kind",
        )?;
        let url = Url::parse(&format!(
            "https://registry-1.docker.io/v2/library/{}/{kind}/{hash}",
            self.repository
        ))?;
        let response = self.response(url)?;
        if let Some(size) = response.content_length() {
            require(size <= maximum, "HTTP content length exceeds limit")?;
            if let Some(expected) = expected {
                require(size == expected.size, "HTTP content length mismatch")?;
            }
        }
        let mut file = OpenOptions::new().write(true).create_new(true).open(path)?;
        let (actual, _) = copy_checked(response, &mut file, maximum, expected)?;
        require(actual == hash, "pinned registry resource digest mismatch")
    }
}

#[derive(Deserialize)]
struct Manifest {
    #[serde(rename = "schemaVersion")]
    schema_version: u32,
    #[serde(rename = "mediaType")]
    media_type: String,
    config: Descriptor,
    layers: Vec<Descriptor>,
}

struct Base {
    config: Value,
    layers: Vec<Descriptor>,
    blobs: BTreeMap<String, PathBuf>,
}

fn read_json(path: &Path) -> Result<Value> {
    require(
        fs::metadata(path)?.len() <= METADATA_LIMIT,
        "JSON metadata exceeds limit",
    )?;
    Ok(serde_json::from_reader(File::open(path)?)?)
}

fn load_base(fetch: &impl Fetch, work: &Path, pinned: &str) -> Result<Base> {
    let root = work.join("base-root.json");
    fetch.fetch("manifests", pinned, &root, METADATA_LIMIT, None)?;
    load_base_root(fetch, work, &root)
}

fn load_base_root(fetch: &impl Fetch, work: &Path, root: &Path) -> Result<Base> {
    let mut document = read_json(root)?;
    if document.get("mediaType").and_then(Value::as_str) == Some(INDEX) {
        require(
            document["schemaVersion"] == 2,
            "invalid index schema version",
        )?;
        let manifests = document["manifests"]
            .as_array()
            .ok_or("missing index manifests")?;
        require(
            !manifests.is_empty() && manifests.len() <= 32,
            "index descriptor limit exceeded",
        )?;
        let candidates: Vec<_> = manifests
            .iter()
            .filter(|entry| {
                entry["platform"]["os"] == "linux"
                    && entry["platform"]["architecture"] == "amd64"
                    && entry["platform"].get("variant").is_none()
            })
            .collect();
        require(
            candidates.len() == 1,
            "base must select exactly one linux/amd64 manifest",
        )?;
        let selected: Descriptor = serde_json::from_value(candidates[0].clone())?;
        require(
            selected.media_type == MANIFEST,
            "unsupported selected manifest media type",
        )?;
        selected.validate(METADATA_LIMIT)?;
        let path = work.join("selected-manifest.json");
        fetch.fetch(
            "manifests",
            &selected.digest,
            &path,
            METADATA_LIMIT,
            Some(&selected),
        )?;
        document = read_json(&path)?;
    }
    let manifest: Manifest = serde_json::from_value(document)?;
    require(
        manifest.schema_version == 2 && manifest.media_type == MANIFEST,
        "unsupported base manifest",
    )?;
    require(
        manifest.config.media_type == CONFIG,
        "unsupported base config media type",
    )?;
    manifest.config.validate(METADATA_LIMIT)?;
    require(
        !manifest.layers.is_empty() && manifest.layers.len() <= 16,
        "base layer count exceeds limit",
    )?;
    let config_path = work.join("base-config.json");
    fetch.fetch(
        "blobs",
        &manifest.config.digest,
        &config_path,
        METADATA_LIMIT,
        Some(&manifest.config),
    )?;
    let config = read_json(&config_path)?;
    require(
        config["os"] == "linux" && config["architecture"] == "amd64",
        "base config architecture mismatch",
    )?;
    require(
        config["rootfs"]["type"] == "layers",
        "unsupported base rootfs",
    )?;
    let diff_ids = config["rootfs"]["diff_ids"]
        .as_array()
        .ok_or("missing base diff IDs")?;
    require(
        diff_ids.len() == manifest.layers.len(),
        "base diff ID count mismatch",
    )?;
    let mut total = 0;
    let mut blobs = BTreeMap::new();
    for (index, layer) in manifest.layers.iter().enumerate() {
        require(
            matches!(layer.media_type.as_str(), LAYER | GZIP_LAYER),
            "unsupported base layer media type",
        )?;
        layer.validate(BLOB_LIMIT)?;
        total += layer.size;
        require(total <= BLOB_LIMIT, "base download budget exceeded")?;
        let path = work.join(format!("base-layer-{index}"));
        fetch.fetch("blobs", &layer.digest, &path, BLOB_LIMIT, Some(layer))?;
        // Verify the config's uncompressed identity independently of compressed
        // distribution digests; bounded inflation also rejects zip bombs.
        let input = layer_reader(&path, layer)?;
        let (actual, _) = copy_checked(input, std::io::sink(), EXPANDED_LIMIT, None)?;
        require(
            diff_ids[index].as_str() == Some(&actual),
            "base diff ID mismatch",
        )?;
        require(
            blobs.insert(layer.digest.clone(), path).is_none(),
            "duplicate base layer",
        )?;
    }
    Ok(Base {
        config,
        layers: manifest.layers,
        blobs,
    })
}

fn layer_reader(path: &Path, descriptor: &Descriptor) -> Result<Box<dyn Read>> {
    let file = File::open(path)?;
    Ok(if descriptor.media_type == GZIP_LAYER {
        Box::new(GzDecoder::new(file))
    } else {
        Box::new(file)
    })
}

/// Bootstraps the existing pinned Python toolchain as verified content, then
/// executes the repository's ordinary assembly script. No image entrypoint or
/// package-maintainer script is executed. The accepted glibc Rust worker supplies
/// the system ABI; this deliberately rejects the Alpine/musl worker image.
pub fn run() -> Result<()> {
    let prefix = Path::new("/workspace/.utf-oci-python");
    if !prefix.exists() {
        let work = tempfile::tempdir_in("/workspace")?;
        let base = load_base(&Registry::new("python")?, work.path(),
            "sha256:8fb0b110b022c70cbe09d3ff1fb313654f31f9c814abb0d48180af56d906a199")?;
        fs::create_dir(prefix)?;
        for layer in &base.layers {
            let mut archive = tar::Archive::new(layer_reader(&base.blobs[&layer.digest], layer)?);
            archive.set_preserve_permissions(true);
            archive.set_preserve_ownerships(false);
            for entry in archive.entries()? {
                let mut entry = entry?;
                let path = entry.path()?.into_owned();
                require(path.components().all(|part| matches!(part, Component::Normal(_) | Component::CurDir)), "unsafe Python image path")?;
                if path.starts_with("usr/local") {
                    require(!path.components().any(|part| part.as_os_str().to_string_lossy().starts_with(".wh.")), "Python whiteout unsupported")?;
                    require(entry.unpack_in(prefix)?, "Python path escaped prefix")?;
                }
            }
        }
    }
    let arguments: Vec<_> = std::env::args_os().skip(1).collect();
    let mut command = std::process::Command::new(prefix.join("usr/local/bin/python3"));
    command.env("PYTHONHOME", prefix.join("usr/local"))
        .env("LD_LIBRARY_PATH", prefix.join("usr/local/lib"));
    if arguments.first().is_some_and(|value| value == "python") {
        command.args(arguments.iter().skip(1));
    } else {
        command.arg(".mozaic/oci-layout/assemble.py").args(arguments);
    }
    let status = command.status()?;
    require(status.success(), "ordinary OCI assembly command failed")
}
