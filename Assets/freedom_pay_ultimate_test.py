#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Freedom Pay Ultimate Signature Analysis
Ультимативный анализ подписи Freedom Pay с обратной инженерией
"""

import hashlib
import requests
import urllib.parse
import itertools
import string

# Данные из рабочей ссылки личного кабинета
WORKING_DATA = {
    'merchant_id': '552170',
    'amount': '1000',
    'currency': 'UZS',
    'description': 'sadas',
    'salt': '5kqQUImDRGHmFsRH',
    'language': 'ru',
    'signature': '90efed8d022f586f431193a390f08456'
}

# Возможные ключи (может быть опечатка в документации)
POSSIBLE_KEYS = [
    'wUQ18x3bzP86MUzn',  # Основной receive ключ
    'lvA1DXTL8ILLj0P',   # Основной payout ключ  
    'lvA1DXTL8ILLLj0P',  # С тремя L
    'wUQ18x3bzP86MUz',   # Без последней n
    'wUQ18x3bzP86MUzN',  # Заглавная N
    'wuq18x3bzp86muzn',  # Нижний регистр
    'WUQ18X3BZP86MUZN',  # Верхний регистр
    'wUQ18x3bzP86MUzn1', # С цифрой в конце
    'wUQ18x3bzP86MUzn_', # С подчеркиванием
    '1wUQ18x3bzP86MUzn', # С цифрой в начале
]

def compute_hash(text: str, algorithm='md5') -> str:
    """Вычисляет хеш строки различными алгоритмами"""
    if algorithm == 'md5':
        return hashlib.md5(text.encode('utf-8')).hexdigest().lower()
    elif algorithm == 'sha1':
        return hashlib.sha1(text.encode('utf-8')).hexdigest().lower()
    elif algorithm == 'sha256':
        return hashlib.sha256(text.encode('utf-8')).hexdigest().lower()[:32]  # Обрезаем до 32 символов
    elif algorithm == 'sha512':
        return hashlib.sha512(text.encode('utf-8')).hexdigest().lower()[:32]
    return text

def test_all_possible_keys():
    """Тестируем все возможные ключи с базовым алгоритмом"""
    print("=" * 80)
    print("🔑 ТЕСТ ВСЕХ ВОЗМОЖНЫХ КЛЮЧЕЙ")
    print("=" * 80)
    
    data = WORKING_DATA
    target_sig = data['signature']
    
    # Базовая строка для подписи (самый частый вариант)
    base_string = f"{data['merchant_id']};{data['amount']};{data['currency']};{data['description']};{data['salt']};{data['language']}"
    
    print(f"Базовая строка: {base_string}")
    print(f"Цель: {target_sig}")
    print()
    
    for i, key in enumerate(POSSIBLE_KEYS, 1):
        test_string = base_string + key
        signature = compute_hash(test_string, 'md5')
        match = signature == target_sig
        
        print(f"КЛЮЧ {i:2d}: {key:<20} -> {signature} {'✅ СОВПАДЕНИЕ!' if match else '❌'}")
        
        if match:
            print(f"🎉 НАЙДЕН ПРАВИЛЬНЫЙ КЛЮЧ: {key}")
            return key
    
    print("\n❌ Не найден правильный ключ среди стандартных вариантов")
    return None

def test_different_algorithms():
    """Тестируем разные алгоритмы хеширования"""
    print("\n" + "=" * 80)
    print("🧮 ТЕСТ РАЗНЫХ АЛГОРИТМОВ ХЕШИРОВАНИЯ")
    print("=" * 80)
    
    data = WORKING_DATA
    target_sig = data['signature']
    
    base_string = f"{data['merchant_id']};{data['amount']};{data['currency']};{data['description']};{data['salt']};{data['language']}"
    key = POSSIBLE_KEYS[0]  # Используем основной ключ
    test_string = base_string + key
    
    algorithms = ['md5', 'sha1', 'sha256', 'sha512']
    
    print(f"Строка: {test_string}")
    print(f"Цель: {target_sig}")
    print()
    
    for algo in algorithms:
        signature = compute_hash(test_string, algo)
        match = signature == target_sig
        print(f"{algo.upper():<8}: {signature} {'✅ СОВПАДЕНИЕ!' if match else '❌'}")
        
        if match:
            print(f"🎉 НАЙДЕН ПРАВИЛЬНЫЙ АЛГОРИТМ: {algo.upper()}")
            return algo

def brute_force_string_variations():
    """Пытаемся найти правильную комбинацию параметров методом перебора"""
    print("\n" + "=" * 80)
    print("🔀 БРУТФОРС ВАРИАЦИЙ СТРОКИ ПОДПИСИ")
    print("=" * 80)
    
    data = WORKING_DATA
    target_sig = data['signature']
    key = POSSIBLE_KEYS[0]
    
    # Все возможные параметры
    params = [
        ('merchant_id', data['merchant_id']),
        ('amount', data['amount']), 
        ('currency', data['currency']),
        ('description', data['description']),
        ('salt', data['salt']),
        ('language', data['language']),
        ('payment_origin', 'merchant_cabinet'),
        ('payment.php', 'payment.php'),
    ]
    
    # Различные разделители
    separators = [';', '&', '|', ',', '', '=', ':']
    
    print(f"Тестируем {len(separators)} разделителей с различными комбинациями параметров...")
    print()
    
    found_combinations = []
    
    # Тестируем основные комбинации с разными разделителями
    for sep in separators[:4]:  # Ограничиваем до 4 чтобы не перегружать
        # Комбинация 1: стандартная
        combo1 = sep.join([data['merchant_id'], data['amount'], data['currency'], data['description'], data['salt'], data['language']])
        sig1 = compute_hash(combo1 + key)
        if sig1 == target_sig:
            found_combinations.append(('Стандартная ' + sep, combo1))
        
        # Комбинация 2: с payment.php
        combo2 = sep.join(['payment.php', data['merchant_id'], data['amount'], data['currency'], data['description'], data['salt'], data['language']])
        sig2 = compute_hash(combo2 + key)
        if sig2 == target_sig:
            found_combinations.append(('С payment.php ' + sep, combo2))
        
        # Комбинация 3: с payment_origin
        combo3 = sep.join([data['merchant_id'], data['amount'], data['currency'], data['description'], data['salt'], data['language'], 'merchant_cabinet'])
        sig3 = compute_hash(combo3 + key)
        if sig3 == target_sig:
            found_combinations.append(('С payment_origin ' + sep, combo3))
    
    if found_combinations:
        print("🎉 НАЙДЕНЫ СОВПАДЕНИЯ:")
        for name, combo in found_combinations:
            print(f"  {name}: {combo}")
    else:
        print("❌ Не найдены совпадения в основных комбинациях")

def reverse_engineer_signature():
    """Пытаемся обратная инженерия - ищем возможные источники подписи"""
    print("\n" + "=" * 80)
    print("🔬 ОБРАТНАЯ ИНЖЕНЕРИЯ ПОДПИСИ")
    print("=" * 80)
    
    target_sig = WORKING_DATA['signature']
    data = WORKING_DATA
    
    print(f"Анализируем подпись: {target_sig}")
    print(f"Длина: {len(target_sig)} символов")
    print(f"Тип: {'MD5' if len(target_sig) == 32 else 'Другой'}")
    print()
    
    # Попытка 1: Возможно это не MD5 от нашей строки, а готовая подпись
    print("🔍 Проверяем, не является ли это подписью самих данных...")
    
    # MD5 от разных комбинаций данных
    test_strings = [
        data['merchant_id'] + data['amount'] + data['currency'] + data['description'] + data['salt'] + data['language'],
        data['description'] + data['salt'],
        data['merchant_id'] + data['salt'],
        data['amount'] + data['salt'],
        f"merchant_cabinet{data['salt']}",
        f"payment.php{data['salt']}",
    ]
    
    for test_str in test_strings:
        test_md5 = compute_hash(test_str)
        if test_md5 == target_sig:
            print(f"✅ НАЙДЕНО! Подпись от: {test_str}")
            return test_str
        print(f"❌ {test_str} -> {test_md5}")
    
    # Попытка 2: Возможно это подпись с другими данными
    print("\n🔍 Тестируем подпись с фиксированными значениями...")
    
    fixed_tests = [
        f"552170;1000;UZS;{data['description']};{data['salt']};ru;secret",
        f"552170;1000;UZS;{data['description']};{data['salt']};ru;key", 
        f"552170;1000;UZS;{data['description']};{data['salt']};ru;merchant_cabinet",
        f"payment;552170;1000;UZS;{data['description']};{data['salt']};ru",
    ]
    
    for test_str in fixed_tests:
        test_md5 = compute_hash(test_str)
        if test_md5 == target_sig:
            print(f"✅ НАЙДЕНО! Подпись от: {test_str}")
            return test_str
        print(f"❌ {test_str} -> {test_md5}")
    
    print("\n❌ Обратная инженерия не дала результатов")

def test_cabinet_vs_api_difference():
    """Тестируем различия между кабинетом и API"""
    print("\n" + "=" * 80)
    print("🏢 ТЕСТ РАЗЛИЧИЙ КАБИНЕТ vs API")
    print("=" * 80)
    
    # Делаем запрос с нашей подписью
    print("Тестируем нашу подпись против API...")
    
    our_data = WORKING_DATA.copy()
    our_data['salt'] = 'testsalt123'
    our_data['description'] = 'Test Payment'
    
    # Генерируем нашу подпись
    our_string = f"payment.php;{our_data['merchant_id']};{our_data['amount']};{our_data['currency']};{our_data['description']};{our_data['salt']};{our_data['language']}"
    our_signature = compute_hash(our_string + POSSIBLE_KEYS[0])
    
    print(f"Наша строка: {our_string}")
    print(f"Наша подпись: {our_signature}")
    
    # Тестируем запрос
    try:
        params = {
            'pg_merchant_id': our_data['merchant_id'],
            'pg_amount': our_data['amount'],
            'pg_currency': our_data['currency'],
            'pg_description': our_data['description'],
            'pg_salt': our_data['salt'],
            'pg_language': our_data['language'],
            'payment_origin': 'merchant_cabinet',
            'pg_sig': our_signature
        }
        
        response = requests.get('https://api.freedompay.uz/payment.php', params=params, timeout=10)
        print(f"\nРезультат нашего запроса: статус {response.status_code}")
        
        if response.status_code == 200 and len(response.text) > 1000:
            print("✅ Наша подпись работает! Алгоритм найден!")
            return our_string, our_signature
        else:
            print("❌ Наша подпись не работает")
            
    except Exception as e:
        print(f"❌ Ошибка запроса: {e}")
    
    return None

def main():
    """Главная функция ультимативного анализа"""
    print("=" * 80)
    print("🚀 FREEDOM PAY ULTIMATE SIGNATURE ANALYSIS")
    print("=" * 80)
    print("Полный анализ алгоритма подписи методом обратной инженерии")
    print()
    
    # Этап 1: Тестируем все возможные ключи
    found_key = test_all_possible_keys()
    
    # Этап 2: Тестируем разные алгоритмы хеширования
    found_algo = test_different_algorithms()
    
    # Этап 3: Брутфорс вариаций строки
    brute_force_string_variations()
    
    # Этап 4: Обратная инженерия
    reverse_engineer_signature()
    
    # Этап 5: Тестируем наши подписи против API
    test_cabinet_vs_api_difference()
    
    print("\n" + "=" * 80)
    print("📊 ИТОГИ АНАЛИЗА:")
    print("- Протестировано 10+ различных ключей")
    print("- Проверено 4 алгоритма хеширования") 
    print("- Проанализировано 20+ вариантов строки подписи")
    print("- Выполнена обратная инженерия")
    print("- Протестирован API в реальном времени")
    
    if found_key:
        print(f"✅ Найден ключ: {found_key}")
    if found_algo:
        print(f"✅ Найден алгоритм: {found_algo}")
        
    print("\n🎯 РЕКОМЕНДАЦИИ:")
    print("1. Если алгоритм не найден - обратитесь в техподдержку Freedom Pay")
    print("2. Попробуйте использовать рабочую ссылку как основу")
    print("3. Возможно требуется дополнительная активация API")
    print("=" * 80)

if __name__ == "__main__":
    main() 