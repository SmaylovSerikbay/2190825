#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Freedom Pay API Signature Analysis Script
Анализ алгоритма подписи для Freedom Pay API
"""

import hashlib
import requests
import urllib.parse
from typing import Dict, Any

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

# Секретные ключи
RECEIVE_SECRET_KEY = 'wUQ18x3bzP86MUzn'
PAYOUT_SECRET_KEY = 'lvA1DXTL8ILLj0P'

def compute_md5(text: str) -> str:
    """Вычисляет MD5 хеш строки"""
    return hashlib.md5(text.encode('utf-8')).hexdigest().lower()

def test_signature_variant(variant_name: str, data_to_sign: str, target_signature: str) -> bool:
    """Тестирует вариант подписи"""
    print(f"\n🔬 [{variant_name}]")
    print(f"   Строка для подписи: {data_to_sign}")
    
    # Тестируем с receive ключом
    receive_sig = compute_md5(data_to_sign + RECEIVE_SECRET_KEY)
    receive_match = receive_sig == target_signature
    
    # Тестируем с payout ключом
    payout_sig = compute_md5(data_to_sign + PAYOUT_SECRET_KEY)
    payout_match = payout_sig == target_signature
    
    # Тестируем с исправленным payout ключом (3 L)
    corrected_payout_key = 'lvA1DXTL8ILLLj0P'
    corrected_sig = compute_md5(data_to_sign + corrected_payout_key)
    corrected_match = corrected_sig == target_signature
    
    print(f"   Receive ключ:     {receive_sig} {'✅ СОВПАДЕНИЕ!' if receive_match else '❌'}")
    print(f"   Payout ключ:      {payout_sig} {'✅ СОВПАДЕНИЕ!' if payout_match else '❌'}")
    print(f"   Исправл. payout:  {corrected_sig} {'✅ СОВПАДЕНИЕ!' if corrected_match else '❌'}")
    
    return receive_match or payout_match or corrected_match

def analyze_signature():
    """Анализирует различные варианты алгоритма подписи"""
    print("=" * 60)
    print("🔍 АНАЛИЗ ПОДПИСИ FREEDOM PAY API")
    print("=" * 60)
    print(f"🎯 Цель: найти алгоритм для подписи {WORKING_DATA['signature']}")
    
    data = WORKING_DATA
    found_match = False
    
    # Группа 1: Стандартные варианты
    variants_group1 = [
        ("ТЕСТ 1: payment.php;merchant_id;amount;currency;description;salt;language", 
         f"payment.php;{data['merchant_id']};{data['amount']};{data['currency']};{data['description']};{data['salt']};{data['language']}"),
        
        ("ТЕСТ 2: без payment.php", 
         f"{data['merchant_id']};{data['amount']};{data['currency']};{data['description']};{data['salt']};{data['language']}"),
        
        ("ТЕСТ 3: другой порядок (salt перед description)", 
         f"{data['merchant_id']};{data['amount']};{data['currency']};{data['salt']};{data['description']};{data['language']}"),
        
        ("ТЕСТ 4: с payment_origin=merchant_cabinet", 
         f"payment.php;{data['merchant_id']};{data['amount']};{data['currency']};{data['description']};{data['salt']};{data['language']};merchant_cabinet"),
    ]
    
    # Группа 2: Разные разделители
    variants_group2 = [
        ("ТЕСТ 5: разделитель &", 
         f"{data['merchant_id']}&{data['amount']}&{data['currency']}&{data['description']}&{data['salt']}&{data['language']}"),
        
        ("ТЕСТ 6: разделитель |", 
         f"{data['merchant_id']}|{data['amount']}|{data['currency']}|{data['description']}|{data['salt']}|{data['language']}"),
        
        ("ТЕСТ 7: разделитель ,", 
         f"{data['merchant_id']},{data['amount']},{data['currency']},{data['description']},{data['salt']},{data['language']}"),
        
        ("ТЕСТ 8: без разделителей (конкатенация)", 
         f"{data['merchant_id']}{data['amount']}{data['currency']}{data['description']}{data['salt']}{data['language']}"),
    ]
    
    # Группа 3: С префиксами pg_
    variants_group3 = [
        ("ТЕСТ 9: с префиксами pg_", 
         f"pg_merchant_id;{data['merchant_id']};pg_amount;{data['amount']};pg_currency;{data['currency']};pg_description;{data['description']};pg_salt;{data['salt']};pg_language;{data['language']}"),
        
        ("ТЕСТ 10: только значения параметров pg_", 
         f"{data['merchant_id']};{data['amount']};{data['currency']};{data['description']};{data['salt']};{data['language']};merchant_cabinet"),
        
        ("ТЕСТ 11: как в URL параметрах", 
         f"pg_merchant_id={data['merchant_id']}&pg_amount={data['amount']}&pg_currency={data['currency']}&pg_description={data['description']}&pg_salt={data['salt']}&pg_language={data['language']}&payment_origin=merchant_cabinet"),
    ]
    
    # Группа 4: Алфавитный порядок и вариации
    variants_group4 = [
        ("ТЕСТ 12: алфавитный порядок по именам параметров", 
         f"{data['amount']};{data['currency']};{data['description']};{data['language']};{data['merchant_id']};{data['salt']}"),
        
        ("ТЕСТ 13: только обязательные параметры", 
         f"{data['merchant_id']};{data['amount']};{data['currency']};{data['salt']}"),
        
        ("ТЕСТ 14: без описания", 
         f"payment.php;{data['merchant_id']};{data['amount']};{data['currency']};{data['salt']};{data['language']}"),
        
        ("ТЕСТ 15: URL encoded описание", 
         f"payment.php;{data['merchant_id']};{data['amount']};{data['currency']};{urllib.parse.quote(data['description'])};{data['salt']};{data['language']}"),
    ]
    
    # Группа 5: Нестандартные варианты
    variants_group5 = [
        ("ТЕСТ 16: с добавлением ключа в начале", 
         f"{RECEIVE_SECRET_KEY};{data['merchant_id']};{data['amount']};{data['currency']};{data['description']};{data['salt']};{data['language']}"),
        
        ("ТЕСТ 17: MD5 без добавления ключа в конце", 
         f"{data['merchant_id']};{data['amount']};{data['currency']};{data['description']};{data['salt']};{data['language']}"),
        
        ("ТЕСТ 18: ключ в середине", 
         f"{data['merchant_id']};{data['amount']};{RECEIVE_SECRET_KEY};{data['currency']};{data['description']};{data['salt']};{data['language']}"),
        
        ("ТЕСТ 19: двойной MD5", 
         f"{data['merchant_id']};{data['amount']};{data['currency']};{data['description']};{data['salt']};{data['language']}"),
    ]
    
    # Тестируем все группы
    all_variants = variants_group1 + variants_group2 + variants_group3 + variants_group4 + variants_group5
    
    for variant_name, variant_string in all_variants:
        if variant_name == "ТЕСТ 17: MD5 без добавления ключа в конце":
            # Специальный случай - MD5 без ключа
            print(f"\n🔬 [{variant_name}]")
            print(f"   Строка для подписи: {variant_string}")
            test_sig = compute_md5(variant_string)
            match = test_sig == data['signature']
            print(f"   MD5 без ключа:    {test_sig} {'✅ СОВПАДЕНИЕ!' if match else '❌'}")
            if match:
                found_match = True
        elif variant_name == "ТЕСТ 19: двойной MD5":
            # Специальный случай - двойной MD5
            print(f"\n🔬 [{variant_name}]")
            print(f"   Строка для подписи: {variant_string}")
            first_md5 = compute_md5(variant_string + RECEIVE_SECRET_KEY)
            double_md5 = compute_md5(first_md5)
            match = double_md5 == data['signature']
            print(f"   Двойной MD5:      {double_md5} {'✅ СОВПАДЕНИЕ!' if match else '❌'}")
            if match:
                found_match = True
        else:
            if test_signature_variant(variant_name, variant_string, data['signature']):
                found_match = True
    
    print("\n" + "=" * 60)
    if found_match:
        print("🎉 НАЙДЕН ПРАВИЛЬНЫЙ АЛГОРИТМ ПОДПИСИ!")
    else:
        print("❌ Не найден правильный алгоритм. Нужны еще тесты...")
    print("=" * 60)

def test_current_implementation():
    """Тестирует текущую реализацию Unity"""
    print("\n" + "=" * 60)
    print("🎲 ТЕСТ ТЕКУЩЕЙ РЕАЛИЗАЦИИ UNITY")
    print("=" * 60)
    
    # Данные из логов Unity
    unity_data = {
        'merchant_id': '552170',
        'amount': '1000',
        'currency': 'UZS',
        'description': 'Тестовый платеж Freedom Pay',
        'salt': '4567b562755d47f2',
        'language': 'ru'
    }
    
    # Алгоритм из Unity (текущий)
    unity_string = f"payment.php;{unity_data['merchant_id']};{unity_data['amount']};{unity_data['currency']};{unity_data['description']};{unity_data['salt']};{unity_data['language']}"
    unity_signature = compute_md5(unity_string + RECEIVE_SECRET_KEY)
    
    print(f"Unity строка: {unity_string}")
    print(f"Unity подпись: {unity_signature}")
    print(f"Ожидаемая из логов: 22190143504e05e488bd9ee2d6d202a0")
    print(f"Совпадает: {'✅' if unity_signature == '22190143504e05e488bd9ee2d6d202a0' else '❌'}")

def test_api_request():
    """Тестирует реальный запрос к API"""
    print("\n" + "=" * 60)
    print("🌐 ТЕСТ РЕАЛЬНОГО API ЗАПРОСА")
    print("=" * 60)
    
    try:
        # Формируем URL как в рабочей ссылке
        params = {
            'pg_merchant_id': WORKING_DATA['merchant_id'],
            'pg_amount': WORKING_DATA['amount'],
            'pg_currency': WORKING_DATA['currency'],
            'pg_description': WORKING_DATA['description'],
            'pg_salt': WORKING_DATA['salt'],
            'pg_language': WORKING_DATA['language'],
            'payment_origin': 'merchant_cabinet',
            'pg_sig': WORKING_DATA['signature']
        }
        
        url = 'https://api.freedompay.uz/payment.php'
        
        print(f"URL: {url}")
        print("Параметры:")
        for key, value in params.items():
            print(f"  {key}: {value}")
        
        print("\nОтправка запроса...")
        response = requests.get(url, params=params, timeout=10)
        
        print(f"Статус: {response.status_code}")
        print(f"Длина ответа: {len(response.text)} символов")
        
        # Проверяем содержимое ответа
        if 'html' in response.text.lower():
            if 'ошибка' in response.text.lower() or 'error' in response.text.lower():
                print("❌ Получена HTML страница с ошибкой")
                if 'код ошибки' in response.text.lower():
                    print("🔍 Найден код ошибки в ответе")
            else:
                print("✅ Получена HTML страница (возможно, форма оплаты)")
        else:
            print("❓ Неизвестный формат ответа")
        
        # Сохраняем ответ для анализа
        with open('freedom_pay_response.html', 'w', encoding='utf-8') as f:
            f.write(response.text)
        print("📁 Ответ сохранен в freedom_pay_response.html")
        
    except requests.exceptions.RequestException as e:
        print(f"❌ Ошибка запроса: {e}")

def generate_test_signature():
    """Генерирует тестовую подпись для проверки"""
    print("\n" + "=" * 60)
    print("🔧 ГЕНЕРАЦИЯ ТЕСТОВОЙ ПОДПИСИ")
    print("=" * 60)
    
    import time
    import random
    import string
    
    # Генерируем новые тестовые данные
    test_salt = ''.join(random.choices(string.ascii_lowercase + string.digits, k=16))
    test_order_id = f"test_{int(time.time())}"
    
    test_data = {
        'merchant_id': '552170',
        'amount': '1000',
        'currency': 'UZS',
        'order_id': test_order_id,
        'description': 'Test Payment',
        'salt': test_salt,
        'language': 'ru'
    }
    
    print("Тестовые данные:")
    for key, value in test_data.items():
        print(f"  {key}: {value}")
    
    # Если мы нашли правильный алгоритм, используем его
    # Пока используем стандартный
    data_to_sign = f"payment.php;{test_data['merchant_id']};{test_data['amount']};{test_data['currency']};{test_data['order_id']};{test_data['description']};{test_data['salt']};{test_data['language']}"
    signature = compute_md5(data_to_sign + RECEIVE_SECRET_KEY)
    
    print(f"\nСтрока для подписи: {data_to_sign}")
    print(f"Подпись: {signature}")
    
    # Генерируем полный URL
    params = {
        'pg_merchant_id': test_data['merchant_id'],
        'pg_amount': test_data['amount'],
        'pg_currency': test_data['currency'],
        'pg_order_id': test_data['order_id'],
        'pg_description': test_data['description'],
        'pg_salt': test_data['salt'],
        'pg_language': test_data['language'],
        'payment_origin': 'merchant_cabinet',
        'pg_sig': signature
    }
    
    url = 'https://api.freedompay.uz/payment.php?' + urllib.parse.urlencode(params)
    print(f"\nТестовый URL:\n{url}")

def main():
    """Главная функция"""
    print("Freedom Pay API Signature Analysis v2.0")
    print("Расширенный анализ подписи API Freedom Pay")
    print("")
    
    # Анализируем различные варианты подписи
    analyze_signature()
    
    # Тестируем текущую реализацию Unity
    test_current_implementation()
    
    # Тестируем реальный API запрос
    test_api_request()
    
    # Генерируем тестовую подпись
    generate_test_signature()

if __name__ == "__main__":
    main() 