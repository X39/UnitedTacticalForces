fn main() {
    if let Err(error) = utf_oci_layout::run() {
        eprintln!("UTF OCI helper failed: {error}");
        std::process::exit(1);
    }
}
