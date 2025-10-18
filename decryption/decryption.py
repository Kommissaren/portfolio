
#!/usr/bin/env python3
"""
decrypt_bruteforce_tool.py

Purpose: local, command-line tool to try decoding a short ciphertext that looks like
an encoded block of bytes, then attempt decryption with several legacy ciphers
(Blowfish, Twofish, CAST, RC2, AES) using short keys and keys derived from
passphrases. Designed for offline use on your machine where you control the
environment and can allocate more time/CPU than this chat environment.

NOTES:
- You must have permission to attempt decryption on any ciphertext you test.
- Recommended packages: pycryptodome, twofish
    pip install pycryptodome twofish

Usage examples:
    python decrypt_bruteforce_tool.py "dRXk=AmWEdZ*9OXa" --max-keylen 4 --charset "a-z0-9"
    python decrypt_bruteforce_tool.py --input-file samples.txt --wordlist common.txt --try-derived-keys

What it does at a high level:
1. Generates several "decoded byte" candidates from the input by trying:
   - base64 variants (replace '*'->'/' or '+'; add padding)
   - base85/ascii85
2. For each decoded byte candidate, attempts decryption with supported ciphers
   and modes (ECB, CBC with zero IV) using:
   - direct short keys from a charset (configurable length)
   - passphrases from a provided wordlist
   - keys derived from passphrases via MD5/SHA1/SHA256 truncation
3. Scores printable plaintexts by a simple English heuristic and writes results
   to CSV for inspection.

This script favors clarity and flexibility over absolute performance.
"""

import argparse
import base64
import binascii
import itertools
import hashlib
import sys
import time
import csv
import os

# Attempt to import crypto libraries. Some (Twofish) may require additional packages.
HAVE_CRYPTO = True
try:
    from Crypto.Cipher import Blowfish, CAST, ARC2, AES
    from Crypto.Util.Padding import unpad
except Exception:
    HAVE_CRYPTO = False

HAVE_TWOFISH = True
try:
    import twofish
except Exception:
    HAVE_TWOFISH = False

BLOCK_SIZES = {
    'Blowfish': 8,
    'CAST': 8,
    'RC2': 8,
    'AES': 16,
    'Twofish': 16
}


def gen_decoded_candidates(ciphertext: str):
    """Try small set of decoding variants and return unique byte strings with tags."""
    variants = set([ciphertext, ciphertext.replace('*', '/'), ciphertext.replace('*', '+'), ciphertext.replace('=', '')])
    candidates = {}
    # base64 variants
    for v in variants:
        for pad in range(4):
            s = v + ('=' * pad)
            try:
                dec = base64.b64decode(s, validate=False)
                candidates.setdefault(dec, []).append(f'b64[{v}]_pad{pad}')
            except Exception:
                pass
    # base85 / ascii85
    try:
        dec = base64.b85decode(ciphertext)
        candidates.setdefault(dec, []).append('b85')
    except Exception:
        pass
    try:
        dec = base64.a85decode(ciphertext)
        candidates.setdefault(dec, []).append('a85')
    except Exception:
        pass

    return list(candidates.items())


def is_printable(bs: bytes):
    try:
        s = bs.decode('latin1')
    except Exception:
        return False
    return all(32 <= ord(c) <= 126 for c in s)


def score_text(s: str):
    """Very small heuristic to prefer likely English-like plaintexts."""
    s_low = s.lower()
    score = 0
    for word in [' the ', ' and ', ' is ', ' you ', ' password', ' pass', ' ']:
        score += s_low.count(word)
    vowel_frac = sum(1 for c in s_low if c in 'aeiou') / max(1, len(s_low))
    score += vowel_frac * 0.1
    # penalize if too many non-alpha
    nonalpha = sum(1 for c in s if not c.isalnum() and c not in " .,:;'-_@#")
    score -= nonalpha * 0.05
    return score


def try_decrypt_with_key(cipher_bytes: bytes, algo: str, mode: str, key: bytes):
    """Attempt decrypt and return plaintext bytes or None. Handles unpadding safely."""
    try:
        if algo == 'Blowfish':
            cipher = Blowfish.new(key, Blowfish.MODE_ECB) if mode == 'ECB' else Blowfish.new(key, Blowfish.MODE_CBC, iv=b'\x00'*8)
            pt = cipher.decrypt(cipher_bytes)
            block = 8
        elif algo == 'CAST':
            cipher = CAST.new(key, CAST.MODE_ECB) if mode == 'ECB' else CAST.new(key, CAST.MODE_CBC, iv=b'\x00'*8)
            pt = cipher.decrypt(cipher_bytes)
            block = 8
        elif algo == 'RC2' or algo == 'ARC2':
            cipher = ARC2.new(key, ARC2.MODE_ECB) if mode == 'ECB' else ARC2.new(key, ARC2.MODE_CBC, iv=b'\x00'*8)
            pt = cipher.decrypt(cipher_bytes)
            block = 8
        elif algo == 'AES':
            cipher = AES.new(key, AES.MODE_ECB) if mode == 'ECB' else AES.new(key, AES.MODE_CBC, iv=b'\x00'*16)
            pt = cipher.decrypt(cipher_bytes)
            block = 16
        elif algo == 'Twofish':
            # twofish module provides block-level encrypt/decrypt via Twofish object
            # it expects key sizes 16/24/32 and works on 16-byte blocks
            tf = twofish.Twofish(key)
            if len(cipher_bytes) % 16 != 0:
                return None
            # decrypt blockwise
            out = bytearray()
            for i in range(0, len(cipher_bytes), 16):
                block_enc = cipher_bytes[i:i+16]
                out.extend(tf.decrypt(block_enc))
            pt = bytes(out)
            block = 16
        else:
            return None
        # try PKCS7 unpad
        try:
            return unpad(pt, block)
        except Exception:
            return pt
    except Exception:
        return None


def derive_keys_from_passphrase(passphrase: str):
    """Return list of derived keys (bytes) suitable for various ciphers.
    Common derivations: md5 (16), sha1 truncated (16/24), sha256 (16/24/32)
    """
    md5k = hashlib.md5(passphrase.encode('latin1')).digest()
    sha1k = hashlib.sha1(passphrase.encode('latin1')).digest()
    sha256k = hashlib.sha256(passphrase.encode('latin1')).digest()
    out = [md5k, sha1k[:16], sha1k[:24], sha256k[:16], sha256k[:24], sha256k[:32]]
    # filter duplicates
    uniq = []
    for k in out:
        if k not in uniq:
            uniq.append(k)
    return uniq


def expand_charset(spec: str):
    """Simple helper to expand a charset spec like a-zA-Z0-9!@#"""
    out = []
    i = 0
    while i < len(spec):
        if i+2 < len(spec) and spec[i+1] == '-':
            for c in range(ord(spec[i]), ord(spec[i+2]) + 1):
                out.append(chr(c))
            i += 3
        else:
            out.append(spec[i])
            i += 1
    return ''.join(out)


def main():
    p = argparse.ArgumentParser(description='Local decrypt/bruteforce helper for legacy ciphers')
    p.add_argument('ciphertext', nargs='?', help='Ciphertext string (e.g. dRXk=AmWEdZ*9OXa)')
    p.add_argument('--input-file', help='File with ciphertexts (one per line)')
    p.add_argument('--wordlist', help='Wordlist file with passphrase candidates')
    p.add_argument('--max-keylen', type=int, default=3, help='Max keylen for brute-forcing charset (default 3)')
    p.add_argument('--charset', default='a-z0-9', help='Charset spec for brute-force ranges (default a-z0-9)')
    p.add_argument('--try-derived-keys', action='store_true', help='Try MD5/SHA1/SHA256-derived keys from passphrases')
    p.add_argument('--output', default='results.csv', help='CSV file for matches')
    p.add_argument('--time-limit', type=int, default=600, help='Total time limit in seconds for run')
    p.add_argument('--ciphers', default='Blowfish,AES,CAST,RC2,Twofish', help='Comma separated list of ciphers to try')
    args = p.parse_args()

    if not args.ciphertext and not args.input_file:
        p.print_help()
        sys.exit(1)

    charset = expand_charset(args.charset)
    ciphers = [c.strip() for c in args.ciphers.split(',') if c.strip()]

    # read passphrases if provided
    passphrases = []
    if args.wordlist:
        with open(args.wordlist, 'r', encoding='utf8', errors='ignore') as f:
            passphrases = [l.strip() for l in f if l.strip()]

    # prepare ciphertext list
    cts = []
    if args.ciphertext:
        cts.append(args.ciphertext.strip())
    if args.input_file:
        with open(args.input_file, 'r', encoding='utf8', errors='ignore') as f:
            cts.extend([l.strip() for l in f if l.strip()])

    start = time.time()
    matches = []

    for ct in cts:
        decoded_list = gen_decoded_candidates(ct)
        print(f"{ct}: produced {len(decoded_list)} decoded byte candidates")
        for dec_bytes, tags in decoded_list:
            if time.time() - start > args.time_limit:
                print('Time limit reached; exiting loop')
                break
            # try passphrase-derived keys first (if any wordlist)
            if passphrases:
                for pp in passphrases:
                    keys = derive_keys_from_passphrase(pp) if args.try_derived_keys else [pp.encode('latin1')]
                    for key in keys:
                        for algo in ciphers:
                            if time.time() - start > args.time_limit:
                                break
                            if algo == 'Twofish' and not HAVE_TWOFISH:
                                continue
                            if not HAVE_CRYPTO and algo != 'Twofish':
                                continue
                            for mode in ['ECB', 'CBC']:
                                pt = try_decrypt_with_key(dec_bytes, algo, mode, key)
                                if pt and is_printable(pt):
                                    s = pt.decode('latin1')
                                    sc = score_text(s)
                                    matches.append((ct, '|'.join(tags), algo, mode, key.hex() if isinstance(key, bytes) else key, s, sc))
            # try brute-forcing short keys from charset
            for L in range(1, args.max_keylen + 1):
                # avoid enormous keyspaces; user controls max-keylen
                for tup in itertools.product(charset, repeat=L):
                    if time.time() - start > args.time_limit:
                        break
                    keystr = ''.join(tup)
                    keybytes = keystr.encode('latin1')
                    # for block ciphers that require fixed key sizes we will try some mappings:
                    key_variants = [keybytes]
                    # if key is short and we have hash functions, try common derivations
                    if args.try_derived_keys:
                        key_variants.extend(derive_keys_from_passphrase(keystr))
                    for key in key_variants:
                        for algo in ciphers:
                            if algo == 'Twofish' and not HAVE_TWOFISH:
                                continue
                            if not HAVE_CRYPTO and algo != 'Twofish':
                                continue
                            for mode in ['ECB', 'CBC']:
                                pt = try_decrypt_with_key(dec_bytes, algo, mode, key)
                                if pt and is_printable(pt):
                                    s = pt.decode('latin1')
                                    sc = score_text(s)
                                    matches.append((ct, '|'.join(tags), algo, mode, key.hex() if isinstance(key, bytes) else key, s, sc))
                if time.time() - start > args.time_limit:
                    break

    # write matches to CSV
    if matches:
        with open(args.output, 'w', newline='', encoding='utf8') as csvfile:
            writer = csv.writer(csvfile)
            writer.writerow(['ciphertext','decoded_tags','algo','mode','key_hex_or_str','plaintext','score'])
            for row in matches:
                writer.writerow(row)
        print(f"Found {len(matches)} matches. Saved to {args.output}")
    else:
        print("No matches found within the searched space/time limits.")


if __name__ == '__main__':
    main()
